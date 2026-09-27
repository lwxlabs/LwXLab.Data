param(
    [string] $PackageDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/release')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packages = (Resolve-Path -LiteralPath $PackageDirectory).Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$work = Join-Path $root ('artifacts/consumers/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
'<Project />' | Set-Content (Join-Path $work 'Directory.Build.props')
function Invoke-DotNet([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed." }
}
$escaped = [Security.SecurityElement]::Escape($packages)
@"
<configuration><packageSources><clear/><add key="release" value="$escaped"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
"@ | Set-Content (Join-Path $work 'NuGet.config')
$ids = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.csproj' | ForEach-Object {
    [string](([xml](Get-Content $_.FullName -Raw)).Project.PropertyGroup.PackageId)
})
foreach ($framework in @('netstandard2.0', 'netstandard2.1')) {
    $directory = Join-Path $work $framework
    New-Item -ItemType Directory -Path $directory | Out-Null
    $references = (Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.csproj' | ForEach-Object {
        $metadata = ([xml](Get-Content $_.FullName -Raw)).Project.PropertyGroup
        if (([string]$metadata.TargetFrameworks -split ';') -contains $framework) {
            "<PackageReference Include=`"$($metadata.PackageId)`" Version=`"$version`"/>"
        }
    }) -join [Environment]::NewLine
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>$framework</TargetFramework></PropertyGroup><ItemGroup>$references</ItemGroup></Project>
"@ | Set-Content (Join-Path $directory 'Consumer.csproj')
    'public static class Consumer { public static string Validate(string name) => LwXLab.Data.DatabaseIdentifier.Validate(name); }' | Set-Content (Join-Path $directory 'Consumer.cs')
    $project = Join-Path $directory 'Consumer.csproj'
    Invoke-DotNet restore $project --configfile (Join-Path $work 'NuGet.config') --packages (Join-Path $work 'packages') --no-cache
    Invoke-DotNet build $project -c Release --no-restore
}
foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    $directory = Join-Path $work $framework
    New-Item -ItemType Directory -Path $directory | Out-Null
    $references = ($ids | ForEach-Object { "<PackageReference Include=`"$_`" Version=`"$version`"/>" }) -join [Environment]::NewLine
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>$framework</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $directory 'Consumer.csproj')
    @'
using LwXLab.Data;
using LwXLab.Data.AI;
using LwXLab.Data.Diagnostics;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddSqliteDbContext<ConsumerContext>("Data Source=" + args[0]);
services.AddLwXDataDiagnostics();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ConsumerContext>();
await context.Database.OpenConnectionAsync();
await context.Database.EnsureCreatedAsync();
context.Rows.Add(new Row { Name = "package-consumer" });
await context.SaveChangesAsync();
await context.Database.ExecuteSqlRawAsync("CREATE TABLE __EFMigrationsHistory (MigrationId TEXT NOT NULL PRIMARY KEY, ProductVersion TEXT NOT NULL)");
await context.Database.ExecuteSqlRawAsync("INSERT INTO __EFMigrationsHistory VALUES ('20260927000000_Consumer', '8.0')");
if ((await context.Rows.SingleAsync()).Name != "package-consumer") throw new Exception("CRUD failed");
if (AiSchemaContextFactory.Create(context, "sqlite").Entities.Count != 1) throw new Exception("AI metadata failed");
if (ConnectionStringRedactor.Redact("Password=secret").Contains("secret")) throw new Exception("Redaction failed");
Console.WriteLine("Packaged consumer passed");
public sealed class ConsumerContext(DbContextOptions<ConsumerContext> options, LwXDbContextDescriptor<ConsumerContext> descriptor) : LwXDbContext<ConsumerContext>(options, descriptor)
{
    public DbSet<Row> Rows => Set<Row>();
}
public sealed class Row { public int Id { get; set; } public string Name { get; set; } = ""; }
'@ | Set-Content (Join-Path $directory 'Program.cs')
    $project = Join-Path $directory 'Consumer.csproj'
    Invoke-DotNet restore $project --configfile (Join-Path $work 'NuGet.config') --packages (Join-Path $work 'packages') --no-cache
    Invoke-DotNet run --project $project -c Release --no-restore -- (Join-Path $directory 'consumer.db')
}
$toolDirectory = Join-Path $work 'tool'
Invoke-DotNet tool install LwXLab.Data.Tool --version $version --tool-path $toolDirectory --configfile (Join-Path $work 'NuGet.config') --no-cache
$tool = Join-Path $toolDirectory 'dotnet-lwx-data'
if ($IsWindows) { $tool += '.exe' }
$safe = & $tool redact --connection 'Host=localhost;Password=consumer-secret'
if ($LASTEXITCODE -ne 0 -or ($safe -join '') -match 'consumer-secret') { throw 'Packaged CLI redaction failed.' }
$connection = 'Data Source=' + (Join-Path $work 'net10.0/consumer.db')
foreach ($command in @('doctor', 'connection', 'schema', 'migrations', 'manifest')) {
    & $tool $command --provider sqlite --connection $connection
    if ($LASTEXITCODE -ne 0) { throw "Packaged CLI $command failed." }
}
$fingerprint = & $tool fingerprint --provider sqlite --connection $connection
if ($LASTEXITCODE -ne 0) { throw 'Packaged CLI fingerprint failed.' }
$expected = Join-Path $work 'expected-schema.txt'
$fingerprint | Select-Object -Skip 1 | Set-Content $expected
& $tool diff --provider sqlite --connection $connection --expected $expected
if ($LASTEXITCODE -ne 0) { throw 'Packaged CLI matching diff failed.' }
'.Missing.Column:TEXT' | Add-Content $expected
& $tool diff --provider sqlite --connection $connection --expected $expected
if ($LASTEXITCODE -ne 5) { throw 'Packaged CLI drift exit code failed.' }
& $tool permissions --provider sqlite --connection $connection --schema main
if ($LASTEXITCODE -ne 4) { throw 'Packaged CLI unsupported-permissions exit code failed.' }
$global:LASTEXITCODE = 0
Write-Host 'All packaged consumers and installed CLI smoke tests passed.'
