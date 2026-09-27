# Migrating from LwXLabs.Supabase.Postgres preview

The original preview package was intentionally superseded before publishing by the provider-extensible `LwXLab.Data` family.

Old:

```csharp
builder.Services.AddSupabaseDbContext<TorserDbContext>(
    connectionString,
    "torser");
```

New convenience API:

```csharp
builder.Services.AddSupabaseDbContext<TorserDbContext>(
    connectionString,
    "torser");
```

The calling code can remain almost identical. The implementation now comes from `LwXLab.Data.Supabase`, and the common infrastructure lives in the provider-neutral packages.

The recommended context base is now:

```csharp
LwXDbContext<TContext>
```

which receives `LwXDbContextDescriptor<TContext>` through dependency injection so the schema does not need to be duplicated inside each context class.
