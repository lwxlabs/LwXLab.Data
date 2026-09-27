using System;
using LwXLab.Data.PostgreSql;
using Npgsql;

namespace LwXLab.Data.Supabase;

public static class SupabaseConnectionString
{
    public static string Create(SupabaseDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var normalized = PostgreSqlConnectionString.Create(options);
        var builder = new NpgsqlConnectionStringBuilder(normalized);
        var mode = Detect(builder);

        if (mode == SupabaseConnectionMode.TransactionPooler && options.DisableAutoPrepareForTransactionPooler)
        {
            builder.MaxAutoPrepare = 0;
        }

        return builder.ConnectionString;
    }

    public static SupabaseConnectionMode Detect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required.", nameof(connectionString));

        return Detect(new NpgsqlConnectionStringBuilder(connectionString));
    }

    public static string DescribeSafely(SupabaseDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new NpgsqlConnectionStringBuilder(Create(options));
        return string.Format(
            "Mode={0};Host={1};Port={2};Database={3};Username={4};SSL Mode={5};Pooling={6};MaxPoolSize={7}",
            Detect(builder),
            builder.Host,
            builder.Port,
            builder.Database,
            builder.Username,
            builder.SslMode,
            builder.Pooling,
            builder.MaxPoolSize);
    }

    private static SupabaseConnectionMode Detect(NpgsqlConnectionStringBuilder builder)
    {
        var host = (builder.Host ?? string.Empty).Trim().ToLowerInvariant();

        if (host.EndsWith(".pooler.supabase.com", StringComparison.Ordinal))
        {
            return builder.Port == 6543
                ? SupabaseConnectionMode.TransactionPooler
                : SupabaseConnectionMode.SessionPooler;
        }

        if (host.StartsWith("db.", StringComparison.Ordinal) &&
            host.EndsWith(".supabase.co", StringComparison.Ordinal))
        {
            return SupabaseConnectionMode.Direct;
        }

        return SupabaseConnectionMode.Unknown;
    }
}
