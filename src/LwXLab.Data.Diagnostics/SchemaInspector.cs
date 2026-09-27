using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LwXLab.Data.Diagnostics;

public static class SchemaInspector
{
    public static async Task<bool?> ExistsAsync(
        DbConnection connection,
        string providerName,
        string schema,
        CancellationToken cancellationToken = default)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(schema)) throw new ArgumentException("Schema is required.", nameof(schema));

        var provider = providerName ?? string.Empty;
        if (provider.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("MySql", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("Maria", StringComparison.OrdinalIgnoreCase) >= 0)
            return null;

        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var command = connection.CreateCommand())
            {
                if (provider.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0)
                    command.CommandText = "SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = '" + Escape(schema.ToUpperInvariant()) + "'";
                else if (provider.IndexOf("SqlServer", StringComparison.OrdinalIgnoreCase) >= 0
                    || provider.IndexOf("SQL Server", StringComparison.OrdinalIgnoreCase) >= 0)
                    command.CommandText = "SELECT COUNT(*) FROM sys.schemas WHERE name = '" + Escape(schema) + "'";
                else if (provider.IndexOf("Postgre", StringComparison.OrdinalIgnoreCase) >= 0
                    || provider.IndexOf("Supabase", StringComparison.OrdinalIgnoreCase) >= 0
                    || provider.IndexOf("Cockroach", StringComparison.OrdinalIgnoreCase) >= 0)
                    command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = '" + Escape(schema) + "'";
                else
                    return null;

                var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) > 0;
            }
        }
        finally
        {
            if (close) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
