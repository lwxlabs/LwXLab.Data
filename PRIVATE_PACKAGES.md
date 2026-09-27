# Private preview packages

The `0.1.0-preview.3` packages are published to the `lwxlabs` GitHub NuGet feed by the manually dispatched `publish-private-packages` workflow. They are preview packages for private use while live checks for standalone PostgreSQL, CockroachDB and Oracle remain outstanding. All 16 packages were confirmed Private and linked to this private repository on the `lwxlabs` account's Packages page.

Feed: `https://nuget.pkg.github.com/lwxlabs/index.json`

For this Windows PC, create a GitHub personal access token **(classic)** with `read:packages` and access to these private packages. The user-level NuGet config already contains a source named `GitHub` at this feed, but its current credential returns HTTP 401. Update that source from PowerShell (the prompt hides the token):

```powershell
$nugetConfig = Join-Path $env:APPDATA 'NuGet\NuGet.Config'
$secureToken = Read-Host 'GitHub classic PAT (read:packages)' -AsSecureString
$token = [System.Net.NetworkCredential]::new('', $secureToken).Password
try {
  dotnet nuget update source GitHub `
    --source 'https://nuget.pkg.github.com/lwxlabs/index.json' `
    --username lwxlabs --password $token --configfile $nugetConfig
  if ($LASTEXITCODE -ne 0) { throw 'Could not update the GitHub source.' }
} finally {
  $token = $null
  $secureToken.Dispose()
}
```

On a different Windows PC without this source, use `dotnet nuget add source` with `--name GitHub` instead of `update source GitHub`; keep the other arguments. On Windows, NuGet encrypts the saved source password for the current user when `--store-password-in-clear-text` is omitted.

Confirm the source is configured and search with prereleases included:

```powershell
dotnet nuget list source --configfile $nugetConfig
dotnet package search LwXLab.Data.Sqlite --exact-match --prerelease `
  --source 'https://nuget.pkg.github.com/lwxlabs/index.json' `
  --configfile $nugetConfig
```

Keep nuget.org enabled for public dependencies. If an application has its own `NuGet.Config` with `<clear />`, add the authenticated GitHub source there as well; that setting overrides user-level sources. In an application project, install only the provider and helpers you need, for example:

```powershell
dotnet add package LwXLab.Data.Sqlite --version 0.1.0-preview.3
dotnet add package LwXLab.Data.Diagnostics --version 0.1.0-preview.3
```

Other package IDs include `LwXLab.Data.Supabase`, `LwXLab.Data.SqlServer`, `LwXLab.Data.MySql`, and `LwXLab.Data.MariaDb`. The .NET tool is `LwXLab.Data.Tool` and runs as `dotnet-lwx-data` after installation. Use a new package version for each subsequent publish; GitHub Packages does not replace an existing version.
