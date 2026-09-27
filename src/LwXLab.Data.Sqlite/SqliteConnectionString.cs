using System;
using Microsoft.Data.Sqlite;

namespace LwXLab.Data.Sqlite;

/// <summary>SQLite connection-string validation and password-safe diagnostics.</summary>
public static class SqliteConnectionString
{
    public static string Create(SqliteDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.Validate();
        return new SqliteConnectionStringBuilder(options.ConnectionString).ConnectionString;
    }

    public static string DescribeSafely(SqliteDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var builder = new SqliteConnectionStringBuilder(Create(options));
        return string.Format(
            "DataSource={0};Mode={1};Cache={2}",
            builder.DataSource,
            builder.Mode,
            builder.Cache);
    }
}
