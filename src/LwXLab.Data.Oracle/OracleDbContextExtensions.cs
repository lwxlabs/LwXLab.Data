#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.Oracle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class OracleDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseOracle<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<OracleDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new OracleDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = OracleConnectionString.Create(options);

        return builder.UseProvider(DatabaseProviderNames.Oracle, db =>
        {
            db.UseOracle(normalized, oracle =>
            {
                oracle.MigrationsHistoryTable(builder.MigrationsHistoryTable, builder.Schema);
            });
        }, validateRegistration: () =>
        {
            DatabaseIdentifier.Validate(builder.Schema, nameof(builder.Schema), 128);
            DatabaseIdentifier.Validate(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable), 128);
        });
    }

    public static IServiceCollection AddOracleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<OracleDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        DatabaseIdentifier.Validate(schema, nameof(schema), 128);

        return services.AddLwXDbContext<TContext>(
            database => database.UseSchema(schema).UseOracle(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddOracleDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<OracleDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        DatabaseIdentifier.Validate(schema, nameof(schema), 128);

        return services.AddLwXDbContextFactory<TContext>(
            database => database.UseSchema(schema).UseOracle(connectionString, configure),
            lifetime);
    }
}
#endif
