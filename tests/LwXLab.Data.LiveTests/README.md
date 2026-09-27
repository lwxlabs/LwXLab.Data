# Live Supabase verification

This executable validates the real .NET provider against a disposable private test schema: migrations and history, CRUD, schema mapping, permissions, diagnostics, drift, contracts, query budgets, metadata-only AI payloads and session/transaction pooler connections.

Create two empty private schemas (`<name>` and `<name>_b`) through an administrator connection. Give a temporary login USAGE and CREATE on only those schemas and CONNECT on `postgres`. Use its session-pooler connection string with a small connection pool. Do not expose these schemas through the Data API and do not use an application's existing schema.

Set `LWX_DATA_LIVE_CONNECTION` and `LWX_DATA_LIVE_SCHEMA` through your local secret store or environment, then run:

```powershell
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net8.0
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net9.0
dotnet run --project tests/LwXLab.Data.LiveTests -c Release -f net10.0
```

On success, tables are removed; afterward, the administrator must drop both private schemas and the temporary role. If a check fails, it intentionally leaves test objects available for inspection; clean those up before rerunning. No administrator credential is stored or reset. This harness is separate from the normal solution so offline unit tests never require a hosted database.
