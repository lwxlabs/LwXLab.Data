# Changelog

## 0.1.0-preview.3

- Verified Supabase against a hosted PostgreSQL database on .NET 8/9/10, including both shared pooler modes; added a reproducible opt-in live verification harness.
- Fixed false-positive drift warnings caused by the configured EF migration-history table. Standardized the shared-project example connection name as `SharedData`.
- Verified Release builds and behavior tests on .NET 8/9/10; added isolated packaged-consumer and installed CLI checks.
- Fixed EF relational dependencies, logging dependency downgrades, diagnostics/health-check logging registration, test compilation, PowerShell pack argument binding, tool-package validation, and the `dotnet lwx-data` command name.
- Redact malformed connection strings entirely to prevent quoted secret fragments leaking; include README-linked guides in packages.
- Renamed the active package/repository family from `LwXLabs.Data` to `LwXLab.Data` across projects, namespaces, tests, samples, tooling and current documentation; historical migration references remain where they identify an older package.
- Aligned release metadata with Localite conventions: pinned .NET SDK/Source Link 10.0.401, repository/project URLs, NuGet source configuration, security/legal files and source metadata.
- Added package-content validation and hardened PowerShell build failure handling.
- Expanded the automated test target matrix to .NET 8, 9 and 10 and added integration-surface tests for provider registration, diagnostics, AI metadata, ASP.NET Core, OpenTelemetry, tooling and production safety.
- Fixed diagnostics and database-contract server-version reads for providers that require an open connection, including multi-part Oracle-style version strings.

- Added opt-in `LwXLab.Data.Diagnostics` with connectivity/migration/schema checks, drift detection, migration safety, environment guards, query diagnostics and query budgets.
- Added `LwXLab.Data.AspNetCore` health-check endpoint and per-request query-budget middleware.
- Added `LwXLab.Data.OpenTelemetry` as a completely optional instrumentation registration package.
- Added `LwXLab.Data.AI` metadata-only schema context, migration review, query payload, seed blueprint and agent manifest features.
- Added database contracts and startup modes with non-blocking defaults.
- Added provider-neutral secret redaction and connection intelligence.
- Added packable `dotnet-lwx-data` doctor/connection/schema/migration-history/redact tool.
- Added a DI interceptor extension point to the EF Core package so optional diagnostics stay separate from provider packages.
- Wired permission and migration-safety switches into diagnostics, added schema existence checks, and added beginner-safe reset/disable-blocking helpers.
- Added CockroachDB support to the CLI and explicit beginner/validation documentation.

## 0.1.0-preview.2

- Added SQLite provider.
- Added MySQL provider using MySQL Connector/NET.
- Added MariaDB provider using MySqlConnector and Pomelo on .NET 8/9.
- Added CockroachDB provider using Npgsql.
- Added Oracle provider using ODP.NET.
- Added schema-capability metadata so schema-free providers do not fake PostgreSQL-style schemas.
- Preserved PostgreSQL, Supabase and SQL Server providers.
- Expanded tests, build scripts and package matrix.

## 0.1.0-preview.1

- Initial provider-neutral core.
- PostgreSQL, Supabase and SQL Server providers.
