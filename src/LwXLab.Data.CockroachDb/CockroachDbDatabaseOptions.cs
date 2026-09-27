using LwXLab.Data.PostgreSql;

namespace LwXLab.Data.CockroachDb;

/// <summary>PostgreSQL-compatible connection options for CockroachDB.</summary>
public sealed class CockroachDbDatabaseOptions : PostgreSqlDatabaseOptions
{
    public CockroachDbDatabaseOptions()
    {
        RequireSsl = true;
    }

    public CockroachDbDatabaseOptions(string connectionString)
        : base(connectionString)
    {
        RequireSsl = true;
    }
}
