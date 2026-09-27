using System;
using MySqlConnector;

namespace LwXLab.Data.MariaDb;

/// <summary>Normalizes MariaDB/MySqlConnector connection strings and hides passwords in diagnostics.</summary>
public static class MariaDbConnectionString
{
    public static string Create(MariaDbDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        var builder = new MySqlConnectionStringBuilder(options.ConnectionString);

        if (options.RequireSsl &&
            (builder.SslMode == MySqlSslMode.None || builder.SslMode == MySqlSslMode.Preferred))
        {
            builder.SslMode = MySqlSslMode.Required;
        }

        return builder.ConnectionString;
    }

    public static string DescribeSafely(MariaDbDatabaseOptions options)
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
