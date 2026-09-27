#if NET8_0 || NET9_0
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.MariaDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace LwXLab.Data;

public static class MariaDbDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseMariaDb<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<MariaDbDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new MariaDbDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = MariaDbConnectionString.Create(options);

        return builder.UseProvider(
            DatabaseProviderNames.MariaDb,
            db =>
            {
                ServerVersion serverVersion = options.ServerVersion == null
                    ? ServerVersion.AutoDetect(normalized)
                    : new MariaDbServerVersion(options.ServerVersion);

                db.UseMySql(normalized, serverVersion, mysql =>
                {
                    mysql.MigrationsHistoryTable(builder.MigrationsHistoryTable);
                });
            },
            validateRegistration: () =>
            {
                DatabaseIdentifier.Validate(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable), 64);
            },
            requiresSchema: false,
            supportsSchema: false);
    }

    public static IServiceCollection AddMariaDbDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<MariaDbDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddLwXDbContext<TContext>(
            database => database.UseMariaDb(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddMariaDbDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<MariaDbDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        return services.AddLwXDbContextFactory<TContext>(
            database => database.UseMariaDb(connectionString, configure),
            lifetime);
    }
}
#endif
