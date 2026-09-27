using System;
using Microsoft.Data.SqlClient;

namespace LwXLab.Data.SqlServer;

/// <summary>Normalizes SQL Server connection strings and avoids exposing passwords in diagnostics.</summary>
public static class SqlServerConnectionString
{
    public static string Create(SqlServerDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        var builder = new SqlConnectionStringBuilder(options.ConnectionString)
        {
            PersistSecurityInfo = false,
            TrustServerCertificate = options.TrustServerCertificate
        };

        // Use the indexer so the source remains compatible across SqlClient versions where Encrypt's CLR type changed.
        builder["Encrypt"] = options.Encrypt;

        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
            builder.ApplicationName = options.ApplicationName;
        if (options.ConnectTimeoutSeconds.HasValue)
            builder.ConnectTimeout = options.ConnectTimeoutSeconds.Value;
        if (options.MaximumPoolSize.HasValue)
            builder.MaxPoolSize = options.MaximumPoolSize.Value;
        if (options.MinimumPoolSize.HasValue)
            builder.MinPoolSize = options.MinimumPoolSize.Value;

        return builder.ConnectionString;
    }

    public static string DescribeSafely(SqlServerDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new SqlConnectionStringBuilder(Create(options));
        return string.Format(
            "DataSource={0};InitialCatalog={1};IntegratedSecurity={2};Encrypt={3};TrustServerCertificate={4};Pooling={5};MaxPoolSize={6}",
            builder.DataSource,
            builder.InitialCatalog,
            builder.IntegratedSecurity,
            builder["Encrypt"],
            builder.TrustServerCertificate,
            builder.Pooling,
            builder.MaxPoolSize);
    }
}
