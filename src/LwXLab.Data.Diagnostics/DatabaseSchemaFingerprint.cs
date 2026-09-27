using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LwXLab.Data.Diagnostics;

public sealed class DatabaseSchemaFingerprint
{
    public DatabaseSchemaFingerprint(string source, string hash, IReadOnlyList<string> entries)
    {
        Source = source;
        Hash = hash;
        Entries = entries;
    }
    public string Source { get; }
    public string Hash { get; }
    public IReadOnlyList<string> Entries { get; }
}

public static class DatabaseDriftDetector
{
    public static DatabaseSchemaFingerprint FromEfModel(DbContext context, string? schema)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var entries = new List<string>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;
            var entitySchema = entity.GetSchema() ?? schema ?? string.Empty;
            var store = StoreObjectIdentifier.Table(table, entitySchema.Length == 0 ? null : entitySchema);
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column == null) continue;
                entries.Add(entitySchema + "." + table + "." + column + ":" + (property.GetColumnType() ?? property.ClrType.FullName));
            }
        }
        entries.Sort(StringComparer.OrdinalIgnoreCase);
        return new DatabaseSchemaFingerprint("ef-model", Hash(entries), entries);
    }

    public static async Task<DatabaseSchemaFingerprint> FromDatabaseAsync(
        DbConnection connection,
        string providerName,
        string? schema,
        CancellationToken cancellationToken = default)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = new List<string>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = BuildColumnQuery(providerName, schema);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var s = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                        var table = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                        var column = reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                        var type = reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                        entries.Add(s + "." + table + "." + column + ":" + type);
                    }
                }
            }
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            return new DatabaseSchemaFingerprint("database", Hash(entries), entries);
        }
        finally
        {
            if (close) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public static IReadOnlyList<string> Diff(DatabaseSchemaFingerprint expected, DatabaseSchemaFingerprint actual)
    {
        var a = new HashSet<string>(expected.Entries, StringComparer.OrdinalIgnoreCase);
        var b = new HashSet<string>(actual.Entries, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var item in a) if (!b.Contains(item)) result.Add("- missing: " + item);
        foreach (var item in b) if (!a.Contains(item)) result.Add("+ unexpected: " + item);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string BuildColumnQuery(string provider, string? schema)
    {
        var normalized = provider ?? string.Empty;
        if (normalized.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0)
            return "SELECT '' AS table_schema, m.name AS table_name, p.name AS column_name, p.type AS data_type FROM sqlite_master m JOIN pragma_table_info(m.name) p WHERE m.type='table' AND m.name NOT LIKE 'sqlite_%' ORDER BY m.name, p.cid";

        if (normalized.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var owner = string.IsNullOrWhiteSpace(schema) ? "USER" : "'" + Escape(schema!.ToUpperInvariant()) + "'";
            return "SELECT OWNER, TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM ALL_TAB_COLUMNS WHERE OWNER = " + owner + " ORDER BY TABLE_NAME, COLUMN_ID";
        }

        var condition = string.IsNullOrWhiteSpace(schema) ? string.Empty : " AND table_schema = '" + Escape(schema!) + "'";
        return "SELECT table_schema, table_name, column_name, data_type FROM information_schema.columns WHERE 1=1" + condition + " ORDER BY table_schema, table_name, ordinal_position";
    }

    private static string Escape(string value) => value.Replace("'", "''");

    private static string Hash(IReadOnlyList<string> entries)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", entries));
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
