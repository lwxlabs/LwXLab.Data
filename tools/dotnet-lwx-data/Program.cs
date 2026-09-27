using System.Data.Common;
using LwXLab.Data;
using LwXLab.Data.MariaDb;
using LwXLab.Data.CockroachDb;
using LwXLab.Data.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LwXLab.Data.MySql;
using LwXLab.Data.Oracle;
using LwXLab.Data.PostgreSql;
using LwXLab.Data.Sqlite;
using LwXLab.Data.SqlServer;
using LwXLab.Data.Supabase;
using LwXLab.Data.Tooling;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Help();
    return 0;
}

var command = args[0].ToLowerInvariant();
var options = Parse(args.Skip(1).ToArray());
var provider = Get(options, "provider") ?? "postgresql";
var connectionString = Get(options, "connection") ?? Environment.GetEnvironmentVariable("LWX_DATA_CONNECTION");

if (command == "redact")
{
    Console.WriteLine(ConnectionStringRedactor.Redact(connectionString));
    return 0;
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Connection string required via --connection or LWX_DATA_CONNECTION.");
    return 2;
}

if (command == "connection")
{
    Console.WriteLine("Provider: " + provider);
    Console.WriteLine("Connection: " + ConnectionStringRedactor.Redact(connectionString));
    foreach (var advice in ConnectionIntelligence.Analyze(provider, connectionString))
        Console.WriteLine(advice.Severity + " " + advice.Code + ": " + advice.Message);
    return 0;
}

await using var connection = CreateConnection(provider, connectionString);
try
{
    await connection.OpenAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Connection failed: " + ex.GetType().Name + ": " + ex.Message);
    return 3;
}

if (command == "doctor")
{
    var report = new DoctorReport { Provider = provider, SafeConnectionString = ConnectionStringRedactor.Redact(connectionString) };
    report.Lines.Add("Connectivity: OK");
    report.Lines.Add("Server version: " + connection.ServerVersion);
    report.Lines.Add("Database: " + connection.Database);
    foreach (var advice in ConnectionIntelligence.Analyze(provider, connectionString))
        report.Lines.Add(advice.Severity + " " + advice.Code + ": " + advice.Message);
    Console.Write(report.ToText());
    return 0;
}

if (command == "permissions")
{
    var schema = Get(options, "schema");
    if (string.IsNullOrWhiteSpace(schema)) { Console.Error.WriteLine("--schema is required for permissions."); return 2; }
    var result = await SchemaIsolationChecker.CheckAsync(connection, provider, schema);
    Console.WriteLine("Supported: " + result.Supported);
    Console.WriteLine("CanUseSchema: " + result.CanUseSchema);
    Console.WriteLine("CanCreateInSchema: " + result.CanCreateInSchema);
    Console.WriteLine(result.Message);
    return result.Supported ? 0 : 4;
}

if (command == "fingerprint" || command == "diff")
{
    var schema = Get(options, "schema");
    var live = await DatabaseDriftDetector.FromDatabaseAsync(connection, provider, schema);
    if (command == "fingerprint")
    {
        Console.WriteLine(live.Hash);
        foreach (var entry in live.Entries) Console.WriteLine(entry);
        return 0;
    }
    var expectedPath = Get(options, "expected");
    if (string.IsNullOrWhiteSpace(expectedPath) || !File.Exists(expectedPath)) { Console.Error.WriteLine("diff requires --expected <fingerprint-lines-file>."); return 2; }
    var expectedEntries = File.ReadAllLines(expectedPath).Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("#")).ToList();
    var expected = new DatabaseSchemaFingerprint("expected", HashLines(expectedEntries), expectedEntries);
    var diff = DatabaseDriftDetector.Diff(expected, live);
    foreach (var line in diff) Console.WriteLine(line);
    return diff.Count == 0 ? 0 : 5;
}

if (command == "manifest")
{
    var schema = Get(options, "schema");
    var payload = new { version = 1, provider, schema, database = connection.Database, serverVersion = connection.ServerVersion };
    Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (command == "schema")
{
    var schema = Get(options, "schema");
    using var cmd = connection.CreateCommand();
    cmd.CommandText = BuildSchemaQuery(provider, schema);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync()) Console.WriteLine(Convert.ToString(reader.GetValue(0)) + "." + Convert.ToString(reader.GetValue(1)));
    return 0;
}

if (command == "migrations")
{
    var schema = Get(options, "schema");
    var table = Get(options, "table") ?? "__EFMigrationsHistory";
    using var cmd = connection.CreateCommand();
    cmd.CommandText = BuildMigrationQuery(provider, schema, table);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync()) Console.WriteLine(Convert.ToString(reader.GetValue(0)));
    return 0;
}

Console.Error.WriteLine("Unknown command: " + command);
Help();
return 2;

static DbConnection CreateConnection(string provider, string connection)
{
    switch (provider.ToLowerInvariant())
    {
        case "postgres": case "postgresql": return new PostgreSqlConnectionFactory(new PostgreSqlDatabaseOptions(connection)).CreateConnection();
        case "supabase": return new SupabaseConnectionFactory(new SupabaseDatabaseOptions(connection)).CreateConnection();
        case "cockroach": case "cockroachdb": return new CockroachDbConnectionFactory(new CockroachDbDatabaseOptions(connection)).CreateConnection();
        case "sqlserver": case "mssql": return new SqlServerConnectionFactory(new SqlServerDatabaseOptions(connection)).CreateConnection();
        case "sqlite": return new SqliteConnectionFactory(new SqliteDatabaseOptions(connection)).CreateConnection();
        case "mysql": return new MySqlConnectionFactory(new MySqlDatabaseOptions(connection)).CreateConnection();
        case "mariadb": return new MariaDbConnectionFactory(new MariaDbDatabaseOptions(connection)).CreateConnection();
        case "oracle": return new OracleConnectionFactory(new OracleDatabaseOptions(connection)).CreateConnection();
        default: throw new ArgumentException("Unknown provider: " + provider);
    }
}

static string BuildSchemaQuery(string provider, string? schema)
{
    if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)) return "SELECT '' AS schema_name, name AS table_name FROM sqlite_master WHERE type='table' ORDER BY name";
    if (provider.Equals("oracle", StringComparison.OrdinalIgnoreCase)) return "SELECT OWNER, TABLE_NAME FROM ALL_TABLES WHERE OWNER = " + (schema == null ? "USER" : "'" + schema.Replace("'", "''").ToUpperInvariant() + "'") + " ORDER BY TABLE_NAME";
    return "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type='BASE TABLE'" + (schema == null ? "" : " AND table_schema='" + schema.Replace("'", "''") + "'") + " ORDER BY table_schema, table_name";
}

static string BuildMigrationQuery(string provider, string? schema, string table)
{
    static string Q(string s) => s.Replace("\"", "\"\"");
    static string B(string s) => s.Replace("]", "]]" );
    if (provider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase) || provider.Equals("mssql", StringComparison.OrdinalIgnoreCase))
        return "SELECT [MigrationId] FROM " + (schema == null ? "" : "[" + B(schema) + "].") + "[" + B(table) + "] ORDER BY [MigrationId]";
    if (provider.Equals("mysql", StringComparison.OrdinalIgnoreCase) || provider.Equals("mariadb", StringComparison.OrdinalIgnoreCase))
        return "SELECT `MigrationId` FROM `" + table.Replace("`", "``") + "` ORDER BY `MigrationId`";
    if (provider.Equals("oracle", StringComparison.OrdinalIgnoreCase))
        return "SELECT \"MigrationId\" FROM " + (schema == null ? "" : "\"" + Q(schema.ToUpperInvariant()) + "\".") + "\"" + Q(table) + "\" ORDER BY \"MigrationId\"";
    return "SELECT \"MigrationId\" FROM " + (schema == null ? "" : "\"" + Q(schema) + "\".") + "\"" + Q(table) + "\" ORDER BY \"MigrationId\"";
}

static Dictionary<string,string> Parse(string[] values)
{
    var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < values.Length; i++)
    {
        if (!values[i].StartsWith("--")) continue;
        var key = values[i].Substring(2);
        if (i + 1 < values.Length && !values[i + 1].StartsWith("--")) result[key] = values[++i];
        else result[key] = "true";
    }
    return result;
}
static string? Get(Dictionary<string,string> values, string key) => values.TryGetValue(key, out var value) ? value : null;
static string HashLines(IEnumerable<string> lines)
{
    using var sha = SHA256.Create();
    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))));
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
static void Help()
{
    Console.WriteLine("dotnet lwx-data <doctor|connection|schema|permissions|migrations|fingerprint|diff|manifest|redact> --provider <provider> --connection <connection> [--schema <schema>]");
    Console.WriteLine("The connection may also be supplied through LWX_DATA_CONNECTION to avoid shell history.");
}
