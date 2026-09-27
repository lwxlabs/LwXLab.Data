# Publishing

## 1. Validate locally

```powershell
.\build\pack.ps1
```

The script stops on the first restore/build/test/pack failure, then runs `build/Test-Packages.ps1` and `build/Test-PackageConsumers.ps1`. Consumers use an isolated package cache and do not inherit repository build properties. Expected packages under `artifacts/release`:

```text
LwXLab.Data.*.nupkg
LwXLab.Data.EntityFrameworkCore.*.nupkg
LwXLab.Data.PostgreSql.*.nupkg
LwXLab.Data.CockroachDb.*.nupkg
LwXLab.Data.SqlServer.*.nupkg
LwXLab.Data.Sqlite.*.nupkg
LwXLab.Data.MySql.*.nupkg
LwXLab.Data.MariaDb.*.nupkg
LwXLab.Data.Oracle.*.nupkg
LwXLab.Data.Supabase.*.nupkg
LwXLab.Data.Diagnostics.*.nupkg
LwXLab.Data.AspNetCore.*.nupkg
LwXLab.Data.OpenTelemetry.*.nupkg
LwXLab.Data.AI.*.nupkg
LwXLab.Data.Tooling.*.nupkg
LwXLab.Data.Tool.*.nupkg
```

Also run:

```powershell
dotnet list .\LwXLab.Data.sln package --vulnerable --include-transitive
dotnet list .\LwXLab.Data.sln package --outdated
```

Review outdated provider packages rather than blindly upgrading across EF major versions.

## 2. Publish in dependency order

1. `LwXLab.Data`
2. `LwXLab.Data.EntityFrameworkCore`
3. Provider packages: `PostgreSql`, `CockroachDb`, `SqlServer`, `Sqlite`, `MySql`, `MariaDb`, `Oracle`
4. `LwXLab.Data.Supabase`
5. `LwXLab.Data.Diagnostics`
6. Optional integrations: `AspNetCore`, `OpenTelemetry`, `AI`
7. `LwXLab.Data.Tooling`
8. `LwXLab.Data.Tool`

Example:

```powershell
dotnet nuget push .\artifacts\release\LwXLab.Data.0.1.0-preview.3.nupkg `
  --api-key $env:NUGET_API_KEY `
  --source https://api.nuget.org/v3/index.json
```

Do not commit a NuGet API key or database credential.

Repository metadata is configured for:

```text
https://github.com/lwxlabs/LwXLab.Data
```

Do not publish unless `build/pack.ps1`, the package-content validation, the vulnerability audit, and the manual provider checks required for the release have passed.
