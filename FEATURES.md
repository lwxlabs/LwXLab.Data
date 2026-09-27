# LwXLab.Data feature modules

The provider packages stay simple. Advanced features are **opt-in** and are not required by a beginner.

## Safe defaults

Installing `LwXLab.Data.PostgreSql`, `LwXLab.Data.SqlServer`, `LwXLab.Data.Supabase`, etc. does not enable diagnostics, telemetry, AI, blocking safety guards, or automatic migrations.

Even after `AddLwXDataDiagnostics()` is called, defaults are observation-only. `UseBeginnerSafeDefaults()` restores those defaults and `DisableBlockingBehavior()` explicitly disables every LwXLab.Data diagnostic guard that can reject an operation or fail startup:

- startup is not blocked;
- destructive SQL is not blocked;
- migrations are not automatically applied;
- query budgets do not throw;
- drift checks and expensive permission checks are disabled;
- SQL text is not logged.

## 1. Diagnostics / database doctor API

```csharp
builder.Services.AddLwXDataDiagnostics();
```

`ILwXDataDiagnostics.RunAsync()` checks connectivity, pending migrations, configured schema and connection advice.

## 2. `dotnet lwx-data`

The repository contains a packable .NET tool:

```powershell
dotnet lwx-data doctor --provider supabase --connection $env:SUPABASE_DB
dotnet lwx-data connection --provider postgresql --connection $env:DB
dotnet lwx-data schema --provider sqlserver --connection $env:DB --schema internal
dotnet lwx-data permissions --provider postgresql --connection $env:DB --schema torser
dotnet lwx-data migrations --provider postgresql --connection $env:DB --schema torser
dotnet lwx-data fingerprint --provider postgresql --connection $env:DB --schema torser
dotnet lwx-data diff --provider postgresql --connection $env:DB --schema torser --expected expected-schema.txt
dotnet lwx-data manifest --provider postgresql --connection $env:DB --schema torser
dotnet lwx-data redact --connection "..."
```

Prefer `LWX_DATA_CONNECTION` or another secret store instead of putting a password in shell history.

## 3. Migration safety

```csharp
var report = await MigrationSafetyAnalyzer.AnalyzeAsync(db);
```

The analyzer inspects the generated pending migration SQL for destructive patterns. To execute with an explicit production guard:

```csharp
await db.MigrateSafelyAsync(DatabaseEnvironment.Production, allowDestructiveMigrations: false);
```

## 4. Schema isolation checks

`SchemaIsolationChecker` checks schema permissions for PostgreSQL/Supabase/CockroachDB and SQL Server. Unsupported providers return `Supported=false` instead of pretending a result.

## 5. Drift detection

`DatabaseDriftDetector` creates an EF-model fingerprint and a live schema fingerprint and can diff them. It is disabled in the automatic diagnostics defaults because schema introspection can be relatively expensive.

## 6. Environment protection

Optional command interception can block obvious `DROP TABLE`, `DROP DATABASE`, `TRUNCATE TABLE`, and `DROP COLUMN` operations in Production.

```csharp
builder.Services.AddLwXDataDiagnostics(o =>
{
    o.EnableEnvironmentProtection = true;
    o.Environment = DatabaseEnvironment.Production;
    o.BlockDestructiveCommandsInProduction = true;
});
```

This is intentionally off by default and complements—not replaces—database permissions and reviewed migrations.

## 7. Connection intelligence

`ConnectionIntelligence.Analyze(...)` reports common connection mistakes and recognizes Supabase session/transaction poolers without exposing passwords.

## 8. Secret protection

`ConnectionStringRedactor.Redact(...)` masks password/token-like fields and is used by tooling output.

## 9. Query diagnostics

Optional EF interception reports slow queries, potentially unbounded SELECTs, large result sets, and repeated-query/N+1-like patterns. SQL text is not logged unless `IncludeSqlTextInLogs=true`.

## 10. Query budgets

For ASP.NET Core:

```csharp
builder.Services.AddLwXDataDiagnostics(o =>
{
    o.EnableQueryBudget = true;
    o.QueryBudgetWarningCount = 20;
    o.QueryBudgetMaximumCount = 50;
    // o.ThrowWhenQueryBudgetExceeded = true; // explicitly opt into blocking
});

app.UseLwXDataQueryBudget();
```

## Health checks

Install `LwXLab.Data.AspNetCore`:

```csharp
builder.Services.AddLwXDataHealthCheck();
app.MapLwXDataHealth("/health/data");
```

## OpenTelemetry

Telemetry exporters are **not** pulled into the normal provider packages. Install `LwXLab.Data.OpenTelemetry` only when wanted:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddLwXDataInstrumentation())
    .WithMetrics(m => m.AddLwXDataInstrumentation());
```

The diagnostics package uses standard .NET `ActivitySource` and `Meter`; query spans are created only when an activity listener is present, and there is no mandatory OpenTelemetry exporter dependency.

## AI / agent features

Install `LwXLab.Data.AI`. It has no OpenAI, Anthropic, or other LLM dependency and never sends data anywhere.

- `AiSchemaContextFactory.Create(...)`: metadata-only database context for agents.
- `AiMigrationReview.From(...)`: structured migration-review payload.
- `AiQueryAnalysis.Create(...)`: explicit SQL/plan payload controlled by the caller.
- `AiDevelopmentSeedBlueprint.Create(...)`: metadata for synthetic development seeds; never reads production rows.
- `LwXDataAgentManifest`: `.lwxlabs/data.json`-style project manifest.

## Database contracts

```csharp
var contract = new DatabaseContract()
    .RequireMinimumServerVersion(new Version(16, 0))
    .RequireTable("Releases", "torser")
    .RequireColumn("Releases", "Version", "torser")
    .RequireExtension("pg_trgm");

var result = await DatabaseContractValidator.ValidateAsync(db, contract);
```

## Startup modes

Startup behavior is separately opt-in:

```csharp
builder.Services.AddLwXDataStartupChecks(o =>
{
    o.Mode = DatabaseStartupMode.Validate;
});
```

Modes: `None`, `Connectivity`, `Validate`, `Migrate`, `Strict`, `Development`.

`None` is the default. Migration-requesting modes still do not execute migrations unless automatic migration execution is explicitly enabled by application code. This prevents a beginner from accidentally turning a health check into a production migration runner.
