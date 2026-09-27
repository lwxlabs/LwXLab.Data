using System;
using LwXLab.Data.PostgreSql;

namespace LwXLab.Data.CockroachDb;

public static class CockroachDbConnectionString
{
    public static string Create(CockroachDbDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        return PostgreSqlConnectionString.Create(options);
    }

    public static string DescribeSafely(CockroachDbDatabaseOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        return PostgreSqlConnectionString.DescribeSafely(options);
    }
}
