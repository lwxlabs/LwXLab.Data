# Validation status for 0.1.0-preview.3

Verified on Windows on 2026-09-27 using .NET SDK 10.0.401.

## Automated verification

- Release restore and solution build pass across every advertised target framework, including the sample and CLI.
- 45 tests pass on each of net8.0, net9.0 and net10.0 (135 executions, no skips).
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

Live PostgreSQL, hosted Supabase/pooler, CockroachDB, SQL Server, MySQL, MariaDB and Oracle integration checks require disposable test endpoints. None were supplied for this preparation pass, so live connectivity, provider-specific migration execution, permissions, SQL dialect behavior and hosted pooler behavior are **not verified**. Registration and connection-string tests are not substitutes for these checks.

MariaDB EF support remains .NET 8/9 only; its .NET 10 package provides ADO.NET support. Linux CI is configured but has not run remotely because this repository has not been pushed.

The source can be uploaded for review, but do not describe all providers/features as integration-tested or publish NuGet packages until the applicable live checks in AGENT_TEST_CHECKLIST.md pass. No Git or NuGet push was performed.
