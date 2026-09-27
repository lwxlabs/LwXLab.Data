using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LwXLab.Data.Diagnostics;

public sealed class SchemaIsolationResult
{
    public bool Supported { get; internal set; }
    public bool CanUseSchema { get; internal set; }
    public bool CanCreateInSchema { get; internal set; }
    public string Message { get; internal set; } = string.Empty;
}

public static class SchemaIsolationChecker
{
    public static async Task<SchemaIsolationResult> CheckAsync(
        DbConnection connection,
        string providerName,
        string schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema)) return new SchemaIsolationResult { Supported = false, Message = "No schema configured." };
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var command = connection.CreateCommand())
            {
                if (providerName.IndexOf("Postgre", StringComparison.OrdinalIgnoreCase) >= 0
                    || providerName.IndexOf("Supabase", StringComparison.OrdinalIgnoreCase) >= 0
                    || providerName.IndexOf("Cockroach", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    command.CommandText = "SELECT has_schema_privilege(current_user, '" + Escape(schema) + "', 'USAGE'), has_schema_privilege(current_user, '" + Escape(schema) + "', 'CREATE')";
                }
                else if (providerName.IndexOf("SqlServer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    command.CommandText = "SELECT HAS_PERMS_BY_NAME('" + Escape(schema) + "', 'SCHEMA', 'SELECT'), HAS_PERMS_BY_NAME('" + Escape(schema) + "', 'SCHEMA', 'ALTER')";
                }
                else
                {
                    return new SchemaIsolationResult { Supported = false, Message = "Provider-specific schema permission checks are not implemented for this provider." };
                }

                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        return new SchemaIsolationResult { Supported = true, Message = "Permission query returned no rows." };
                    return new SchemaIsolationResult
                    {
                        Supported = true,
                        CanUseSchema = Convert.ToBoolean(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                        CanCreateInSchema = Convert.ToBoolean(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                        Message = "Schema permissions read successfully."
                    };
                }
            }
        }
        finally
        {
            if (close) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
