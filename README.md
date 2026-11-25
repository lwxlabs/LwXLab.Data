# LwXLab.Data

`LwXLab.Data` is a modular .NET data-access infrastructure toolkit. It standardizes provider registration, connections, EF Core schemas/migration history and optional production diagnostics without replacing EF Core or ADO.NET.

## Providers

- `LwXLab.Data.PostgreSql`
- `LwXLab.Data.Supabase`
- `LwXLab.Data.CockroachDb`
- `LwXLab.Data.SqlServer`
- `LwXLab.Data.Sqlite`
- `LwXLab.Data.MySql`
- `LwXLab.Data.MariaDb`
- `LwXLab.Data.Oracle`

Additional optional packages:

- `LwXLab.Data.Diagnostics`
- `LwXLab.Data.AspNetCore`
- `LwXLab.Data.OpenTelemetry`
- `LwXLab.Data.AI`
- `LwXLab.Data.Tooling`
- `LwXLab.Data.Tool` (`dotnet lwx-data`)

## Simple usage

```csharp
builder.Services.AddSupabaseDbContext<CatalogDbContext>(connectionString, "catalog");
builder.Services.AddSqlServerDbContext<InternalDbContext>(sqlServer, "internal");
```

Your application continues using ordinary EF Core `DbContext` APIs.

## Beginner-safe by design

Provider packages do not automatically enable telemetry, AI, startup checks, query interception, migration execution, or destructive-operation blocking.

Advanced functionality is separately installed/registered. Even `AddLwXDataDiagnostics()` defaults to reporting rather than blocking. SQL text is not logged by default.

Start with [BEGINNER_QUICKSTART.md](BEGINNER_QUICKSTART.md). See [FEATURES.md](FEATURES.md) for diagnostics, `dotnet lwx-data doctor`, migration safety, schema isolation, drift detection, environment protection, connection intelligence, secret redaction, query diagnostics, query budgets, health checks, OpenTelemetry, AI agent context, database contracts, and startup modes.

## Framework targets

The provider-neutral/ADO.NET packages target the frameworks supported by their underlying database drivers, including `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, and `net10.0` where available. Modern EF Core, diagnostics, ASP.NET Core, AI metadata helpers, and OpenTelemetry packages target .NET 8/9/10.

## Build and pack

Requires the .NET 10 SDK. Source builds restore from NuGet.org and do not require private feed credentials.

```powershell
.\build\pack.ps1
```

The pack scripts restore, build, run the tests on .NET 8/9/10, pack every public package, validate NuGet contents, build clean package consumers for .NET Standard 2.0/2.1, run consumers on .NET 8/9/10, and install and smoke-test the CLI. See `AGENT_TEST_CHECKLIST.md` for provider/integration smoke checks and `VALIDATION.md` for the latest verification status.

## License and security

Licensed under the [Apache License 2.0](LICENSE.txt). See [NOTICE.txt](NOTICE.txt) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for additional notices. Security reports should follow [SECURITY.md](SECURITY.md).

Copyright © 2025–2026 lwxlabs.
