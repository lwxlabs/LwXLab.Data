# Live database verification

This executable validates the real .NET provider against a disposable private test schema: migrations and history, CRUD, schema mapping, permissions, diagnostics, drift, contracts, query budgets, metadata-only AI payloads and session/transaction pooler connections.

## SQLite, MySQL and MariaDB on this PC

From the repository root, run `./build/Test-LocalProviders.ps1` in PowerShell. Use `-Provider Sqlite`, `-Provider MySql`, or `-Provider MariaDb` to select one provider. The script creates a separate SQLite file for each .NET target and fresh MySQL 8.4/MariaDB 10.11 Docker containers with a loopback-only random host port and random temporary password. It removes each file/container afterward, including on test failure. Docker Desktop must be running for MySQL/MariaDB, and the official images are pulled on first use. It never starts or modifies existing application containers.

The live checks cover provider connection factories, EF Core migrations/rollback and CRUD, diagnostics, schema drift, contracts, query budgets and metadata-only AI context. MySQL and SQLite are checked on .NET 8/9/10. MariaDB EF Core is checked on .NET 8/9; its .NET 10 package currently offers ADO.NET only, so .NET 10 checks connection and CRUD without EF Core. Local MariaDB uses `SslMode=None` on a loopback-only disposable port; this does not change production TLS defaults. See the test source for the exact checks.

Create two empty private schemas (`<name>` and `<name>_b`) through an administrator connection. Give a temporary login USAGE and CREATE on only those schemas and CONNECT on `postgres`. Use its session-pooler connection string with a small connection pool. Do not expose these schemas through the Data API and do not use an application's existing schema.

Set `LWX_DATA_LIVE_CONNECTION` and `LWX_DATA_LIVE_SCHEMA` through your local secret store or environment, then run:

```powershell
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net8.0
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net9.0
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net10.0
```

On success, tables are removed; afterward, the administrator must drop both private schemas and the temporary role. If a check fails, it intentionally leaves test objects available for inspection; clean those up before rerunning. No administrator credential is stored or reset. This harness is separate from the normal solution so offline unit tests never require a hosted database.

## Local SQL Server Express

On Windows, use your existing SQL Server instance and Windows authentication:

```powershell
./build/Test-LocalSqlServer.ps1 -Server '.\SQLEXPRESS' -TrustServerCertificate
```

The certificate switch is explicit and intended for a local development instance with a self-signed certificate. Omit it when the instance has a trusted certificate. Neither the server configuration nor the provider's certificate-validation defaults are changed. The CLI has the equivalent explicit `--trust-server-certificate` option.

The script creates a uniquely named disposable database, verifies the .NET 8/9/10 SQL Server provider and CLI, and drops only that generated database in a `finally` block. It additionally verifies detection of text-length and decimal-precision drift. It does not stop or uninstall SQL Server and does not alter existing application databases. Logs remain under ignored `artifacts/local-server`.
