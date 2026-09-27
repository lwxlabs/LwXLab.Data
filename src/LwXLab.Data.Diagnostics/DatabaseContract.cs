using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LwXLab.Data.Diagnostics;

public sealed class DatabaseContract
{
    internal List<DatabaseContractRequirement> Requirements { get; } = new List<DatabaseContractRequirement>();
    internal Version? MinimumServerVersion { get; private set; }
    internal List<string> RequiredExtensions { get; } = new List<string>();

    public DatabaseContract RequireTable(string table, string? schema = null)
    {
        Requirements.Add(new DatabaseContractRequirement("table", schema, table, null));
        return this;
    }

    public DatabaseContract RequireColumn(string table, string column, string? schema = null)
    {
        Requirements.Add(new DatabaseContractRequirement("column", schema, table, column));
        return this;
    }

    public DatabaseContract RequireMinimumServerVersion(Version version)
    {
        MinimumServerVersion = version ?? throw new ArgumentNullException(nameof(version));
        return this;
    }

    /// <summary>Requires a database extension. Currently validated for PostgreSQL-compatible providers.</summary>
    public DatabaseContract RequireExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("Extension name is required.", nameof(extension));
        RequiredExtensions.Add(extension.Trim());
        return this;
    }
}

internal sealed class DatabaseContractRequirement
{
    public DatabaseContractRequirement(string kind, string? schema, string table, string? column)
    { Kind = kind; Schema = schema; Table = table; Column = column; }
    public string Kind { get; }
    public string? Schema { get; }
    public string Table { get; }
    public string? Column { get; }
}

public sealed class DatabaseContractResult
{
    public List<string> Missing { get; } = new List<string>();
    public List<string> Unsupported { get; } = new List<string>();
    public bool IsSatisfied => Missing.Count == 0 && Unsupported.Count == 0;
}

public static class DatabaseContractValidator
{
    /// <summary>Validates requirements against live database metadata; it does not mutate the database.</summary>
    public static async Task<DatabaseContractResult> ValidateAsync(DbContext context, DatabaseContract contract, string? schema = null, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (contract == null) throw new ArgumentNullException(nameof(contract));

        var provider = context.Database.ProviderName ?? string.Empty;
        var connection = context.Database.GetDbConnection();
        var live = await DatabaseDriftDetector.FromDatabaseAsync(connection, provider, schema, cancellationToken).ConfigureAwait(false);
        var entries = live.Entries;
        var result = new DatabaseContractResult();

        foreach (var requirement in contract.Requirements)
        {
            var effectiveSchema = requirement.Schema ?? schema;
            var prefix = (effectiveSchema ?? string.Empty) + "." + requirement.Table + ".";
            var tableFound = entries.Any(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(effectiveSchema) && x.IndexOf("." + requirement.Table + ".", StringComparison.OrdinalIgnoreCase) >= 0));
            if (!tableFound)
            {
                result.Missing.Add("table " + (effectiveSchema == null ? string.Empty : effectiveSchema + ".") + requirement.Table);
                continue;
            }

            if (requirement.Kind == "column" && requirement.Column != null)
            {
                var needle = "." + requirement.Table + "." + requirement.Column + ":";
                if (!entries.Any(x => x.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                    result.Missing.Add("column " + requirement.Table + "." + requirement.Column);
            }
        }

        if (contract.MinimumServerVersion != null)
        {
            var serverVersion = await DbConnectionMetadata.ReadServerVersionAsync(connection, cancellationToken).ConfigureAwait(false);
            var current = ParseLeadingVersion(serverVersion);
            if (current == null)
                result.Unsupported.Add("Could not parse server version '" + serverVersion + "'.");
            else if (current < contract.MinimumServerVersion)
                result.Missing.Add("server version >= " + contract.MinimumServerVersion + " (actual " + current + ")");
        }

        if (contract.RequiredExtensions.Count > 0)
        {
            if (provider.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0
                || provider.IndexOf("Postgre", StringComparison.OrdinalIgnoreCase) >= 0
                || provider.IndexOf("Supabase", StringComparison.OrdinalIgnoreCase) >= 0
                || provider.IndexOf("Cockroach", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var close = connection.State != ConnectionState.Open;
                if (close) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (var extension in contract.RequiredExtensions)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = '" + extension.Replace("'", "''") + "'";
                            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                            if (Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
                                result.Missing.Add("extension " + extension);
                        }
                    }
                }
                finally
                {
                    if (close) await connection.CloseAsync().ConfigureAwait(false);
                }
            }
            else
            {
                result.Unsupported.Add("Extension checks are currently implemented only for PostgreSQL-compatible providers.");
            }
        }

        return result;
    }

    private static Version? ParseLeadingVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = new System.Text.StringBuilder();
        var started = false;
        foreach (var c in value)
        {
            if (char.IsDigit(c) || (started && c == '.'))
            {
                digits.Append(c);
                started = true;
            }
            else if (started)
            {
                break;
            }
        }

        var parts = digits.ToString().Trim('.').Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var normalized = string.Join(".", parts.Take(4));
        return Version.TryParse(normalized, out var version) ? version : null;
    }
}
