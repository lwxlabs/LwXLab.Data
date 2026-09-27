using LwXLab.Data.PostgreSql;

namespace LwXLab.Data.Supabase;

/// <summary>Supabase-specific PostgreSQL options.</summary>
public sealed class SupabaseDatabaseOptions : PostgreSqlDatabaseOptions
{
    public SupabaseDatabaseOptions()
    {
        RequireSsl = true;
    }

    public SupabaseDatabaseOptions(string connectionString)
        : base(connectionString)
    {
        RequireSsl = true;
    }

    /// <summary>
    /// When the Supabase shared transaction pooler is detected, force Npgsql automatic preparation off.
    /// Supabase transaction pooling does not support prepared statements.
    /// </summary>
    public bool DisableAutoPrepareForTransactionPooler { get; set; } = true;
}
