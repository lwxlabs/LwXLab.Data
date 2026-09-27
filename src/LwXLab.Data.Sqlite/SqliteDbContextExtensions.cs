#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class SqliteDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseSqlite<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<SqliteDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new SqliteDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = SqliteConnectionString.Create(options);

        return builder.UseProvider(
            DatabaseProviderNames.Sqlite,
            db => db.UseSqlite(normalized, sqlite =>
            {
                sqlite.MigrationsHistoryTable(builder.MigrationsHistoryTable);
            }),
            validateRegistration: () =>
            {
                DatabaseIdentifier.Validate(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable), 128);
            },
            requiresSchema: false,
            supportsSchema: false);
    }

    public static IServiceCollection AddSqliteDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<SqliteDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddLwXDbContext<TContext>(
            database => database.UseSqlite(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddSqliteDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<SqliteDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        return services.AddLwXDbContextFactory<TContext>(
            database => database.UseSqlite(connectionString, configure),
            lifetime);
    }
}
#endif
