using System;
using LwXLab.Data;
using Microsoft.EntityFrameworkCore;

namespace LwXLab.Data.EntityFrameworkCore;

/// <summary>Resolved registration metadata for one DbContext type.</summary>
public sealed class LwXDbContextDescriptor<TContext> : ILwXDbContextDescriptor
    where TContext : DbContext
{
    public LwXDbContextDescriptor(
        string name,
        string providerName,
        string? schema,
        string migrationsHistoryTable = "__EFMigrationsHistory")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A database registration name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("A provider name is required.", nameof(providerName));

        Name = name.Trim();
        ProviderName = providerName.Trim();
        Schema = schema == null ? null : DatabaseIdentifier.Validate(schema, nameof(schema));
        MigrationsHistoryTable = DatabaseIdentifier.Validate(migrationsHistoryTable, nameof(migrationsHistoryTable));
    }

    public Type ContextType => typeof(TContext);
    public string Name { get; }
    public string ProviderName { get; }
    public string? Schema { get; }
    public bool HasSchema => Schema != null;
    public string MigrationsHistoryTable { get; }
}
