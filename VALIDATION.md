# Validation status for 0.1.0-preview.3

Verified on Windows on 2026-09-27 using .NET SDK 10.0.401.

## Automated verification

- Release restore and solution build (also checked with warnings treated as errors: zero warnings/errors) pass across every advertised target framework, including the sample and CLI.
- 46 tests pass on each of net8.0, net9.0 and net10.0 (138 executions, no skips).
- Tests cover provider connection normalization and registration, SQLite CRUD, factory registration, connectivity diagnostics, schema drift, contracts and connection ownership, query budget observation/throwing and nesting, explicit production command/delete guards, pending destructive migration classification/blocking, startup None/Validate/Strict modes, health-status mapping, OpenTelemetry registration, AI metadata/seed/manifest output, and secret redaction including malformed quoted strings.
- All 16 NuGet packages contain expected framework assemblies and README/license/notice/security/repository metadata.
- Clean consumers restore from the produced packages with an isolated package cache and independent build properties. .NET Standard 2.0/2.1 consumers compile; .NET 8/9/10 consumers execute SQLite CRUD and metadata checks.
- The packaged CLI installs and its redaction, doctor, connection, schema, migrations, fingerprint, diff, manifest and unsupported-permissions behavior is checked with SQLite.
- The solution-wide NuGet vulnerability audit, including transitive dependencies, reports no vulnerable packages in the configured feed.
- Outdated dependencies were reviewed: most newer versions cross framework/provider major versions and are intentionally not substituted. Test tooling updates are not needed to resolve a test failure.
- Package/assembly/namespace/repository naming uses LwXLab.Data. Historical old-name references in the changelog describe the rename.
- .gitignore is copied verbatim from the local Localite repository. Build products, package files, IDE state, local NuGet configuration, environment files and validation logs are ignored.

Reproduce the build/test/package/consumer/CLI checks with `./build/pack.ps1` or `./build/pack.sh`. NuGet audit commands are documented in PUBLISHING.md. Local validation logs and packages are retained in ignored files and artifacts directories.

## Remaining release verification

Hosted Supabase verification passed through the real .NET library on .NET 8/9/10: schema-local migrations and migration history, CRUD, second-app schema separation, schema permissions, diagnostics, drift detection (including the migration-history regression fix), database contracts, query budgets, metadata-only AI context, and session/transaction pooler queries. The live CLI doctor/connection/schema/permissions/migrations/fingerprint/manifest checks also passed. Temporary test schemas, login and connector migration records were removed and cleanup was verified. The new free project was retained; the existing paused project was untouched. Reproduce with `tests/LwXLab.Data.LiveTests/README.md`.

Local SQL Server Express 2022 (16.0.1200.5) verification passed with Windows authentication on .NET 8/9/10: migrations and schema-local history, CRUD, schema separation/permissions, diagnostics, drift (including text-length and decimal-precision changes), contracts, budgets and metadata-only AI output. Local CLI doctor/schema/permissions/migrations/fingerprint/manifest checks passed. The explicit development certificate-trust option was tested, and the default CLI correctly rejected the untrusted certificate. Both generated test databases were removed and absence verified; the installed server and existing application databases were left intact. Reproduce using `build/Test-LocalSqlServer.ps1 -TrustServerCertificate`.

Standalone PostgreSQL, CockroachDB and Oracle live integration checks remain **not verified**. Supabase results do not establish the behavior of those separate services/providers.

MariaDB EF support remains .NET 8/9 only; its .NET 10 package provides ADO.NET support. Linux CI is configured; its remote result has not been checked in this validation pass.

Do not describe all providers/features as integration-tested or publish NuGet packages until the applicable live checks in AGENT_TEST_CHECKLIST.md pass. The earlier source commits were pushed to GitHub; the changes in this validation pass remain local.

## Additional live provider verification

`./build/Test-LocalProviders.ps1` exercises disposable local SQLite, MySQL 8.4 and MariaDB 10.11 databases. SQLite and MySQL passed provider factory, migrations and rollback, CRUD, diagnostics, drift, contracts, budgets and AI metadata on .NET 8/9/10. MariaDB passed the same EF Core checks on .NET 8/9 and ADO.NET CRUD on .NET 10; EF Core registration is not supplied for MariaDB on .NET 10. The script removes only its generated files and Docker containers. Details: `tests/LwXLab.Data.LiveTests/README.md`.
