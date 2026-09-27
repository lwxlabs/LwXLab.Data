$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts/release"
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Invoke-DotNet([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}

Invoke-DotNet restore (Join-Path $root "LwXLab.Data.sln")
Invoke-DotNet build (Join-Path $root "LwXLab.Data.sln") -c Release --no-restore
Invoke-DotNet test (Join-Path $root "LwXLab.Data.sln") -c Release --no-build --no-restore

Get-ChildItem (Join-Path $root "src") -Filter *.csproj -Recurse | ForEach-Object {
    Invoke-DotNet pack $_.FullName -c Release --no-build --no-restore --output $out
}
Invoke-DotNet pack (Join-Path $root "tools/dotnet-lwx-data/dotnet-lwx-data.csproj") -c Release --no-build --no-restore --output $out

& (Join-Path $PSScriptRoot "Test-Packages.ps1") -PackageDirectory $out
if ($LASTEXITCODE -ne 0) { throw "Package validation failed with exit code $LASTEXITCODE." }
Write-Host "Packages: $out"
& (Join-Path $PSScriptRoot "Test-PackageConsumers.ps1") -PackageDirectory $out
