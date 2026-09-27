param(
    [string] $Server = '.\SQLEXPRESS',
    [switch] $TrustServerCertificate
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$database = 'LwXLab_Data_Verify_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$output = Join-Path $root ('artifacts/local-server/' + $database)
New-Item -ItemType Directory -Force -Path $output | Out-Null
function Invoke-Sql([string] $Catalog, [string] $Query) {
    $arguments = @('-S', $Server, '-E', '-d', $Catalog, '-b', '-l', '15', '-W', '-Q', $Query)
    if ($TrustServerCertificate) { $arguments += '-C' }
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "SQL Server command failed in $Catalog." }
}
function Invoke-DotNet([string[]] $Arguments, [string] $Log) {
    & dotnet @Arguments *> $Log
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed; inspect $Log." }
}
$environmentNames = @('LWX_DATA_LIVE_CONNECTION','LWX_DATA_LIVE_SCHEMA','LWX_DATA_LIVE_PROVIDER','LWX_DATA_LIVE_TRUST_LOCAL_CERTIFICATE','LWX_DATA_CONNECTION')
$previous = @{}
foreach ($name in $environmentNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$created = $false
try {
    Invoke-Sql master "CREATE DATABASE [$database];"
    $created = $true
    Invoke-Sql $database 'CREATE SCHEMA lwx_local;'
    Invoke-Sql $database 'CREATE SCHEMA lwx_local_b;'
    $env:LWX_DATA_LIVE_CONNECTION = "Server=$Server;Database=$database;Integrated Security=True;Encrypt=True;Application Name=LwXLab.Data.LocalVerify;Max Pool Size=3;Connect Timeout=15"
    $env:LWX_DATA_LIVE_SCHEMA = 'lwx_local'
    $env:LWX_DATA_LIVE_PROVIDER = 'SqlServer'
    $env:LWX_DATA_LIVE_TRUST_LOCAL_CERTIFICATE = $(if ($TrustServerCertificate) { '1' } else { '0' })
    foreach ($framework in @('net8.0','net9.0','net10.0')) {
        Invoke-DotNet @('run','--project',(Join-Path $root 'tests/LwXLab.Data.LiveTests'),'-c','Release','-f',$framework) (Join-Path $output "$framework.log")
        Write-Host "$framework local SQL Server verification passed."
    }
    Invoke-Sql $database "CREATE TABLE lwx_local.Probe (Id uniqueidentifier PRIMARY KEY, Name nvarchar(max) NOT NULL, Amount decimal(18,2) NOT NULL); CREATE TABLE lwx_local.__EFMigrationsHistory (MigrationId nvarchar(150) PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL); INSERT INTO lwx_local.__EFMigrationsHistory VALUES ('20260927000100_LiveProbe', '10.0.12');"
    $env:LWX_DATA_CONNECTION = $env:LWX_DATA_LIVE_CONNECTION
    Invoke-DotNet @('build',(Join-Path $root 'tools/dotnet-lwx-data'),'-c','Release') (Join-Path $output 'cli-build.log')
    $tool = Join-Path $root 'tools/dotnet-lwx-data/bin/Release/net8.0/dotnet-lwx-data.dll'
    foreach ($command in @('doctor','schema','permissions','migrations','fingerprint','manifest')) {
        $arguments = @($tool,$command,'--provider','sqlserver','--schema','lwx_local')
        if ($TrustServerCertificate) { $arguments += '--trust-server-certificate' }
        Invoke-DotNet $arguments (Join-Path $output "cli-$command.log")
    }
    Write-Host "Local SQL Server and CLI verification passed. Logs: $output"
}
finally {
    try {
        if ($created) {
            if ($database -notmatch '^LwXLab_Data_Verify_[0-9a-f]{12}$') { throw 'Refusing to clean up an unexpected database name.' }
            Invoke-Sql master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; IF DB_ID(N'$database') IS NOT NULL THROW 50000, 'Test database cleanup failed', 1;"
            Write-Host "Removed temporary database $database."
        }
    }
    finally {
        foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
}
