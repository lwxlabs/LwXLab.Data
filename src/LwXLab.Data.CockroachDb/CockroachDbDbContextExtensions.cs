#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.CockroachDb;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class CockroachDbDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseCockroachDb<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<CockroachDbDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new CockroachDbDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = CockroachDbConnectionString.Create(options);

        return builder.UseProvider(DatabaseProviderNames.CockroachDb, db =>
        {
            db.UseNpgsql(normalized, npgsql =>
            {
                npgsql.MigrationsHistoryTable(builder.MigrationsHistoryTable, builder.Schema);
                if (options.EnableRetryOnFailure)
                    npgsql.EnableRetryOnFailure(options.MaxRetryCount, options.MaxRetryDelay, null);
            });
        }, validateRegistration: () =>
        {
            DatabaseIdentifier.ValidatePostgreSql(builder.Schema, nameof(builder.Schema));
            DatabaseIdentifier.ValidatePostgreSql(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable));
        });
    }

    public static IServiceCollection AddCockroachDbDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<CockroachDbDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        DatabaseIdentifier.ValidatePostgreSql(schema, nameof(schema));

        return services.AddLwXDbContext<TContext>(
            database => database.UseSchema(schema).UseCockroachDb(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddCockroachDbDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<CockroachDbDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        DatabaseIdentifier.ValidatePostgreSql(schema, nameof(schema));

        return services.AddLwXDbContextFactory<TContext>(
            database => database.UseSchema(schema).UseCockroachDb(connectionString, configure),
            lifetime);
    }
}
#endif
