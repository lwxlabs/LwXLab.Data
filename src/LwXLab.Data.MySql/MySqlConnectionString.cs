using System;
using MySql.Data.MySqlClient;

namespace LwXLab.Data.MySql;

/// <summary>Normalizes MySQL Connector/NET connection strings and hides passwords in diagnostics.</summary>
public static class MySqlConnectionString
{
    public static string Create(MySqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        var builder = new MySqlConnectionStringBuilder(options.ConnectionString)
        {
            PersistSecurityInfo = false
        };

        if (options.RequireSsl &&
            (builder.SslMode == MySqlSslMode.Disabled || builder.SslMode == MySqlSslMode.Preferred))
        {
            builder.SslMode = MySqlSslMode.Required;
        }

        return builder.ConnectionString;
    }

    public static string DescribeSafely(MySqlDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new MySqlConnectionStringBuilder(Create(options));
        return string.Format(
            "Server={0};Port={1};Database={2};UserID={3};SslMode={4};Pooling={5}",
            builder.Server,
            builder.Port,
            builder.Database,
            builder.UserID,
            builder.SslMode,
            builder.Pooling);
    }
}
