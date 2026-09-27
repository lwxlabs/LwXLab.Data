using System;
using Microsoft.EntityFrameworkCore;
using LwXLab.Data;

namespace LwXLab.Data.EntityFrameworkCore;

/// <summary>
/// Provider-extensible registration builder. Provider packages add UsePostgreSql,
/// UseSqlServer, UseSqlite, UseMySql, UseOracle and similar methods.
/// </summary>
public sealed class LwXDbContextBuilder<TContext>
    where TContext : DbContext
{
    private Action<DbContextOptionsBuilder>? _providerConfigurator;
    private Action? _providerRegistrationValidator;
    private string? _providerName;
    private string? _schema;
    private bool _providerRequiresSchema = true;
    private bool _providerSupportsSchema = true;

    internal LwXDbContextBuilder(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? typeof(TContext).Name : name;
    }

    public string Name { get; private set; }

    /// <summary>
    /// Gets the configured schema. Throws when the current registration has no schema.
    /// Use <see cref="SchemaOrNull"/> for providers such as SQLite and MySQL.
    /// </summary>
    public string Schema => _schema
        ?? throw new InvalidOperationException("A schema has not been configured for this database registration.");

    /// <summary>Gets the configured schema, or null for providers that do not use schemas.</summary>
    public string? SchemaOrNull => _schema;

    public bool HasSchema => _schema != null;

    public string MigrationsHistoryTable { get; private set; } = "__EFMigrationsHistory";

    public LwXDbContextBuilder<TContext> UseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A database registration name is required.", nameof(name));

        Name = name.Trim();
        return this;
    }

    public LwXDbContextBuilder<TContext> UseSchema(string schema)
    {
        _schema = DatabaseIdentifier.Validate(schema, nameof(schema));
        return this;
    }

    public LwXDbContextBuilder<TContext> UseMigrationsHistoryTable(string tableName)
    {
        MigrationsHistoryTable = DatabaseIdentifier.Validate(tableName, nameof(tableName));
        return this;
    }

    /// <summary>
    /// Provider extension point. Applications normally call a provider method such as
    /// UsePostgreSql, UseSqlServer, UseSqlite, UseMySql, UseOracle or UseSupabase.
    /// </summary>
    public LwXDbContextBuilder<TContext> UseProvider(
        string providerName,
        Action<DbContextOptionsBuilder> configureProvider,
        Action? validateRegistration = null,
        bool requiresSchema = true,
        bool supportsSchema = true)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("A provider name is required.", nameof(providerName));
        if (configureProvider == null)
            throw new ArgumentNullException(nameof(configureProvider));
        if (_providerConfigurator != null)
            throw new InvalidOperationException($"A database provider is already configured for {typeof(TContext).Name}.");
        if (requiresSchema && !supportsSchema)
            throw new ArgumentException("A provider cannot require a schema while declaring that schemas are unsupported.");

        _providerName = providerName.Trim();
        _providerConfigurator = configureProvider;
        _providerRegistrationValidator = validateRegistration;
        _providerRequiresSchema = requiresSchema;
        _providerSupportsSchema = supportsSchema;
        return this;
    }

    internal void Validate()
    {
        if (_providerConfigurator == null || _providerName == null)
            throw new InvalidOperationException($"No database provider was configured for {typeof(TContext).Name}.");

        if (_providerRequiresSchema && _schema == null)
            throw new InvalidOperationException(
                $"Provider '{_providerName}' requires a schema for {typeof(TContext).Name}. Call UseSchema(...).");

        if (!_providerSupportsSchema && _schema != null)
            throw new InvalidOperationException(
                $"Provider '{_providerName}' does not support EF schema isolation. Remove UseSchema(...) and isolate with a database/catalog or file instead.");

        _providerRegistrationValidator?.Invoke();
    }

    internal void ConfigureProvider(DbContextOptionsBuilder optionsBuilder)
    {
        Validate();
        _providerConfigurator!(optionsBuilder);
    }

    internal LwXDbContextDescriptor<TContext> BuildDescriptor()
    {
        Validate();
        return new LwXDbContextDescriptor<TContext>(Name, _providerName!, _schema, MigrationsHistoryTable);
    }
}
