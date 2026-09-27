using System;

namespace LwXLab.Data.MySql;

/// <summary>Connection options for MySQL Connector/NET.</summary>
public sealed class MySqlDatabaseOptions
{
    public MySqlDatabaseOptions()
    {
    }

    public MySqlDatabaseOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; } = string.Empty;
    public bool RequireSsl { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("A MySQL connection string is required.", nameof(ConnectionString));
    }
}
