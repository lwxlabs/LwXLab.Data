#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class MySqlDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseMySql<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<MySqlDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new MySqlDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = MySqlConnectionString.Create(options);

        return builder.UseProvider(
            DatabaseProviderNames.MySql,
            db => db.UseMySQL(normalized, mysql =>
            {
                mysql.MigrationsHistoryTable(builder.MigrationsHistoryTable);
            }),
            validateRegistration: () =>
            {
                DatabaseIdentifier.Validate(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable), 64);
            },
            requiresSchema: false,
            supportsSchema: false);
    }

    public static IServiceCollection AddMySqlDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<MySqlDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddLwXDbContext<TContext>(
            database => database.UseMySql(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddMySqlDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<MySqlDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        return services.AddLwXDbContextFactory<TContext>(
            database => database.UseMySql(connectionString, configure),
            lifetime);
    }
}
#endif
