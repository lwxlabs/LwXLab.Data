using System;
using Npgsql;

namespace LwXLab.Data.PostgreSql;

/// <summary>Normalizes Npgsql connection strings and avoids exposing passwords in diagnostics.</summary>
public static class PostgreSqlConnectionString
{
    public static string Create(PostgreSqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString)
        {
            PersistSecurityInfo = false
        };

        if (options.RequireSsl &&
            (builder.SslMode == SslMode.Disable || builder.SslMode == SslMode.Allow || builder.SslMode == SslMode.Prefer))
        {
            builder.SslMode = SslMode.Require;
        }

        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
            builder.ApplicationName = options.ApplicationName;
        if (options.CommandTimeoutSeconds.HasValue)
            builder.CommandTimeout = options.CommandTimeoutSeconds.Value;
        if (options.ConnectTimeoutSeconds.HasValue)
            builder.Timeout = options.ConnectTimeoutSeconds.Value;
        if (options.MaximumPoolSize.HasValue)
            builder.MaxPoolSize = options.MaximumPoolSize.Value;
        if (options.MinimumPoolSize.HasValue)
            builder.MinPoolSize = options.MinimumPoolSize.Value;

        return builder.ConnectionString;
    }

    public static string DescribeSafely(PostgreSqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new NpgsqlConnectionStringBuilder(Create(options));
        return string.Format(
            "Host={0};Port={1};Database={2};Username={3};SSL Mode={4};Pooling={5};MaxPoolSize={6}",
            builder.Host,
            builder.Port,
            builder.Database,
            builder.Username,
            builder.SslMode,
            builder.Pooling,
            builder.MaxPoolSize);
    }
}
