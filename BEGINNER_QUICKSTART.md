# Beginner quick start

You do **not** need diagnostics, OpenTelemetry, AI, health checks, the CLI, or startup validation to use LwXLab.Data.

Install only the provider you need and register your `DbContext`.

## Supabase / PostgreSQL

```csharp
builder.Services.AddSupabaseDbContext<TorserDbContext>(
    builder.Configuration.GetConnectionString("SharedData")!,
    schema: "torser");
```

`SharedData` is the logical connection name for a shared Supabase project. Use a separate schema and database role for each application/service; schema mapping alone does not enforce permissions. Store each app's password in its secret store or `ConnectionStrings__SharedData` environment variable. The sample uses the session pooler (port 5432) for persistent servers on IPv4 networks. Use the transaction pooler (6543) for short-lived workloads; the Supabase provider disables automatic preparation there. Migrations should run through the direct endpoint where reachable, or the session pooler for IPv4-only networks.

## SQL Server

```csharp
builder.Services.AddSqlServerDbContext<AppDbContext>(
    builder.Configuration.GetConnectionString("App")!,
    schema: "app");
```

Then inject and use your `DbContext` exactly like normal EF Core.

## Add safe diagnostics later

```csharp
builder.Services.AddLwXDataDiagnostics();
```

The default diagnostics settings are observation-only. They do not auto-migrate, block destructive commands, fail startup, throw because of a query budget, or log SQL text.

You can explicitly restore these defaults at any time:

```csharp
builder.Services.AddLwXDataDiagnostics(options =>
{
    options.UseBeginnerSafeDefaults();
});
```

If an application has customized diagnostics but you want to guarantee that no LwXLab.Data diagnostic feature can block execution:

```csharp
builder.Services.AddLwXDataDiagnostics(options =>
{
    // other reporting features may remain enabled
    options.DisableBlockingBehavior();
});
```

## Advanced features are separate

Install/register them only when needed:

- `LwXLab.Data.AspNetCore` for database health checks and request query budgets.
- `LwXLab.Data.OpenTelemetry` for explicit tracing/metrics registration.
- `LwXLab.Data.AI` for metadata-only agent context and deterministic review payloads.
- `LwXLab.Data.Tool` for `dotnet lwx-data` diagnostics.

None of these are required for ordinary EF Core usage.
