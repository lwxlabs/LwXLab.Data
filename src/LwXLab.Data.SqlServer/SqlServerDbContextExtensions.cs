#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class SqlServerDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseSqlServer<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<SqlServerDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new SqlServerDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = SqlServerConnectionString.Create(options);

        return builder.UseProvider(DatabaseProviderNames.SqlServer, db =>
        {
            db.UseSqlServer(normalized, sql =>
            {
                sql.MigrationsHistoryTable(builder.MigrationsHistoryTable, builder.Schema);
                if (options.EnableRetryOnFailure)
                {
                    sql.EnableRetryOnFailure(options.MaxRetryCount, options.MaxRetryDelay, null);
                }
            });
        }, validateRegistration: () =>
        {
            DatabaseIdentifier.Validate(builder.Schema, nameof(builder.Schema), 128);
            DatabaseIdentifier.Validate(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable), 128);
        });
    }

    public static IServiceCollection AddSqlServerDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<SqlServerDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        DatabaseIdentifier.Validate(schema, nameof(schema), 128);

        return services.AddLwXDbContext<TContext>(
            database => database
                .UseSchema(schema)
                .UseSqlServer(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddSqlServerDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<SqlServerDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        DatabaseIdentifier.Validate(schema, nameof(schema), 128);

        return services.AddLwXDbContextFactory<TContext>(
            database => database
                .UseSchema(schema)
                .UseSqlServer(connectionString, configure),
            lifetime);
    }
}
#endif
