using System;

namespace LwXLab.Data.MariaDb;

/// <summary>Connection options for MariaDB using MySqlConnector.</summary>
public sealed class MariaDbDatabaseOptions
{
    public MariaDbDatabaseOptions()
    {
    }

    public MariaDbDatabaseOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; } = string.Empty;
    public bool RequireSsl { get; set; } = true;

#if NET8_0 || NET9_0
    /// <summary>
    /// Optional server version used by Pomelo. When null, the provider auto-detects it from the database.
    /// Example: new Version(11, 4, 0).
    /// </summary>
    public Version? ServerVersion { get; set; }
#endif

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("A MariaDB connection string is required.", nameof(ConnectionString));
    }
}
