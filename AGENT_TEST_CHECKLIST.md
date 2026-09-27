# Agent test checklist — 0.1.0-preview.3

Verified automated results and explicit live-provider limits are recorded in VALIDATION.md (2026-09-27). Unchecked items remain unverified.

## Build / package
- [x] `dotnet restore LwXLab.Data.sln`
- [x] `dotnet build LwXLab.Data.sln -c Release`
- [x] `dotnet test LwXLab.Data.sln -c Release --no-build` passes on net8.0, net9.0 and net10.0.
- [x] `./build/pack.ps1` creates every packable provider/module and `LwXLab.Data.Tool`.
- [x] `build/Test-Packages.ps1` verifies README/license/notice/security metadata, repository URL and expected framework assemblies in every NuGet package.

## Provider smoke tests
- [ ] PostgreSQL registration + migration history schema.
- [ ] Supabase registration + transaction-pooler auto-prepare protection.
- [ ] SQL Server registration + schema.
- [ ] SQLite registration without schema.
- [ ] MySQL registration without schema.
- [ ] MariaDB ADO.NET all advertised TFMs and EF where supported.
- [ ] CockroachDB PostgreSQL compatibility path.
- [ ] Oracle advertised targets.

## Advanced modules (must remain optional)
- [ ] A project using only a provider package has no dependency on Diagnostics, ASP.NET Core, OpenTelemetry or AI.
- [ ] `AddLwXDataDiagnostics()` does not block startup, auto-migrate, block destructive SQL, throw on budgets, or log SQL by default.
- [ ] Connectivity/migration/schema diagnostics report correctly.
- [ ] Drift detector finds an intentional schema difference.
- [ ] MigrationSafetyAnalyzer classifies DROP TABLE/DROP COLUMN as High.
- [ ] Production destructive-command guard blocks only when explicitly enabled.
- [ ] ConnectionStringRedactor never exposes password/token fields.
- [ ] Supabase transaction pooler is detected by ConnectionIntelligence.
- [ ] Slow-query warning works when enabled.
- [ ] Repeated-query warning works inside a query budget scope.
- [ ] Query budget warning works; exception only when explicitly enabled.
- [ ] ASP.NET health endpoint reports healthy/degraded/unhealthy.
- [ ] `UseLwXDataQueryBudget()` creates one budget scope per request.
- [ ] OpenTelemetry package registers only LwXLab.Data ActivitySource/Meter and requires explicit installation.
- [ ] AI schema context contains metadata but no connection string or row data.
- [ ] AI migration review maps deterministic safety findings.
- [ ] Development seed blueprint does not query data or insert anything.
- [ ] Agent manifest serializes provider/context/schema/migration table metadata.
- [ ] DatabaseContract detects missing table/column requirements.
- [ ] Startup mode `None` performs no work; `Strict` blocks only on failures when explicitly selected.

## CLI
- [ ] Pack/install `LwXLab.Data.Tool` as a local tool.
- [ ] `dotnet lwx-data redact` redacts a password.
- [ ] `doctor` connects and prints server version without exposing secrets.
- [ ] `connection` prints connection advice.
- [ ] `schema` lists tables for at least PostgreSQL, SQL Server and SQLite.
- [ ] `migrations` lists EF migration-history rows.

## Compatibility
- [ ] Validate every advertised target framework with a clean package restore.
- [ ] Check EF Core 8, 9, and 10 samples independently.
- [ ] Run `dotnet list package --vulnerable --include-transitive`.
- [ ] Run `dotnet list package --outdated` and review rather than blindly upgrading provider versions.
