#if NET8_0_OR_GREATER
using System;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.Supabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class SupabaseDbContextExtensions
{
    public static LwXDbContextBuilder<TContext> UseSupabase<TContext>(
        this LwXDbContextBuilder<TContext> builder,
        string connectionString,
        Action<SupabaseDatabaseOptions>? configure = null)
        where TContext : DbContext
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new SupabaseDatabaseOptions(connectionString);
        configure?.Invoke(options);
        options.Validate();
        var normalized = SupabaseConnectionString.Create(options);

        return builder.UseProvider(DatabaseProviderNames.Supabase, db =>
        {
            db.UseNpgsql(normalized, npgsql =>
            {
                npgsql.MigrationsHistoryTable(builder.MigrationsHistoryTable, builder.Schema);
                if (options.EnableRetryOnFailure)
                {
                    npgsql.EnableRetryOnFailure(options.MaxRetryCount, options.MaxRetryDelay, null);
                }
            });
        }, validateRegistration: () =>
        {
            DatabaseIdentifier.ValidatePostgreSql(builder.Schema, nameof(builder.Schema));
            DatabaseIdentifier.ValidatePostgreSql(builder.MigrationsHistoryTable, nameof(builder.MigrationsHistoryTable));
        });
    }

    public static IServiceCollection AddSupabaseDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<SupabaseDatabaseOptions>? configure = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        DatabaseIdentifier.ValidatePostgreSql(schema, nameof(schema));

        return services.AddLwXDbContext<TContext>(
            database => database
                .UseSchema(schema)
                .UseSupabase(connectionString, configure),
            contextLifetime,
            optionsLifetime);
    }

    public static IServiceCollection AddSupabaseDbContextFactory<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<SupabaseDatabaseOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        DatabaseIdentifier.ValidatePostgreSql(schema, nameof(schema));

        return services.AddLwXDbContextFactory<TContext>(
            database => database
                .UseSchema(schema)
                .UseSupabase(connectionString, configure),
            lifetime);
    }
}
#endif
