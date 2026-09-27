using System;
using Oracle.ManagedDataAccess.Client;

namespace LwXLab.Data.Oracle;

/// <summary>Normalizes ODP.NET connection strings and avoids returning passwords in diagnostics.</summary>
public static class OracleConnectionString
{
    public static string Create(OracleDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        var builder = new OracleConnectionStringBuilder(options.ConnectionString)
        {
            PersistSecurityInfo = false
        };

        if (options.ConnectionTimeoutSeconds.HasValue)
            builder.ConnectionTimeout = options.ConnectionTimeoutSeconds.Value;
        if (options.MaximumPoolSize.HasValue)
            builder.MaxPoolSize = options.MaximumPoolSize.Value;
        if (options.MinimumPoolSize.HasValue)
            builder.MinPoolSize = options.MinimumPoolSize.Value;

        return builder.ConnectionString;
    }

    public static string DescribeSafely(OracleDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new OracleConnectionStringBuilder(Create(options));
        return string.Format(
            "DataSource={0};UserID={1};Pooling={2};MinPoolSize={3};MaxPoolSize={4}",
            builder.DataSource,
            builder.UserID,
            builder.Pooling,
            builder.MinPoolSize,
            builder.MaxPoolSize);
    }
}
