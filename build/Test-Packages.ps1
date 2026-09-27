param(
    [string] $PackageDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts/release")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$packageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$props = [xml](Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw)
$version = [string]$props.Project.PropertyGroup.Version
$repoUrl = "https://github.com/lwxlabs/LwXLab.Data"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Test-LibraryPackage([string] $projectPath) {
    $project = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $packageId = [string]($project.Project.PropertyGroup.PackageId | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($packageId)) { return }

    $frameworkText = [string]($project.Project.PropertyGroup.TargetFrameworks | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($frameworkText)) {
        $frameworkText = [string]($project.Project.PropertyGroup.TargetFramework | Select-Object -First 1)
    }
    $frameworks = @($frameworkText -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $packagePath = Join-Path $packageDirectory ($packageId + "." + $version + ".nupkg")
    if (-not (Test-Path -LiteralPath $packagePath)) { throw "Missing package: $packagePath" }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entries = @($archive.Entries | ForEach-Object FullName)
        foreach ($required in @("LICENSE.txt", "NOTICE.txt", "THIRD-PARTY-NOTICES.txt", "SECURITY.md", "README.md")) {
            if ($entries -notcontains $required) { throw "$packageId is missing '$required'." }
        }
        foreach ($framework in $frameworks) {
            $assembly = "lib/$framework/$packageId.dll"
            if ($entries -notcontains $assembly) { throw "$packageId is missing '$assembly'." }
        }

        $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if ($null -eq $nuspecEntry) { throw "$packageId has no .nuspec." }
        $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
        try { $nuspecText = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($nuspecText -notmatch [regex]::Escape($repoUrl)) { throw "$packageId nuspec does not contain repository URL '$repoUrl'." }
        if ($nuspecText -notmatch 'Apache-2\.0') { throw "$packageId nuspec does not contain Apache-2.0 license metadata." }
        if ($nuspecText -notmatch '<readme>README\.md</readme>') { throw "$packageId nuspec does not declare README.md." }
    }
    finally { $archive.Dispose() }
}

Get-ChildItem (Join-Path $root "src") -Filter *.csproj -Recurse | ForEach-Object { Test-LibraryPackage $_.FullName }

$toolPath = Join-Path $packageDirectory ("LwXLab.Data.Tool." + $version + ".nupkg")
if (-not (Test-Path -LiteralPath $toolPath)) { throw "Missing tool package: $toolPath" }
$toolArchive = [System.IO.Compression.ZipFile]::OpenRead($toolPath)
try {
    $toolEntries = @($toolArchive.Entries | ForEach-Object FullName)
    foreach ($required in @("LICENSE.txt", "NOTICE.txt", "THIRD-PARTY-NOTICES.txt", "SECURITY.md", "README.md")) {
        if ($toolEntries -notcontains $required) { throw "LwXLab.Data.Tool is missing '$required'." }
    }
    if (-not ($toolEntries | Where-Object { $_ -match '^tools/net8\.0/any/dotnet-lwx-data\.dll$' })) {
        throw "LwXLab.Data.Tool does not contain the dotnet tool assembly under tools/net8.0/any/."
    }
}
finally { $toolArchive.Dispose() }

Write-Host "All LwXLab.Data packages contain the expected frameworks, legal/security files and repository metadata."
