# Private preview packages

The `0.1.0-preview.3` packages are published to the `lwxlabs` GitHub NuGet feed by the manually dispatched `publish-private-packages` workflow. They are preview packages for private use while live checks for standalone PostgreSQL, CockroachDB and Oracle remain outstanding. All 16 packages were confirmed Private and linked to this private repository on the `lwxlabs` account's Packages page.

Feed: `https://nuget.pkg.github.com/lwxlabs/index.json`

For a Windows development PC, create a GitHub personal access token **(classic)** with `read:packages` and access to these private packages. Then add a user-level source in PowerShell (use your own GitHub username):

```powershell
$env:GH_PACKAGES_TOKEN = Read-Host 'GitHub classic PAT (read:packages)'
dotnet nuget add source 'https://nuget.pkg.github.com/lwxlabs/index.json' `
  --name lwxlabs-github --username YOUR_GITHUB_USERNAME `
  --password $env:GH_PACKAGES_TOKEN `
  --configfile "$env:APPDATA\NuGet\NuGet.Config"
Remove-Item Env:GH_PACKAGES_TOKEN
```

On Windows, NuGet encrypts the saved source password for the current user when `--store-password-in-clear-text` is omitted. Keep nuget.org enabled for public dependencies. If an application has its own `NuGet.Config` with `<clear />`, add the authenticated GitHub source there as well; that setting overrides user-level sources. In an application project, install only the provider and helpers you need, for example:

```powershell
dotnet add package LwXLab.Data.Sqlite --version 0.1.0-preview.3
dotnet add package LwXLab.Data.Diagnostics --version 0.1.0-preview.3
```

Other package IDs include `LwXLab.Data.Supabase`, `LwXLab.Data.SqlServer`, `LwXLab.Data.MySql`, and `LwXLab.Data.MariaDb`. The .NET tool is `LwXLab.Data.Tool` and runs as `dotnet-lwx-data` after installation. Use a new package version for each subsequent publish; GitHub Packages does not replace an existing version.
