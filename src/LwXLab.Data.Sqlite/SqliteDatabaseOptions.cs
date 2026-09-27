using System;

namespace LwXLab.Data.Sqlite;

/// <summary>Connection options for SQLite.</summary>
public sealed class SqliteDatabaseOptions
{
    public SqliteDatabaseOptions()
    {
    }

    public SqliteDatabaseOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("A SQLite connection string is required.", nameof(ConnectionString));
    }
}
