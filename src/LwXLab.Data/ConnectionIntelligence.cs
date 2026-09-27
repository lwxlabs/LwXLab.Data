using System;
using System.Collections.Generic;
using System.Data.Common;

namespace LwXLab.Data;

public enum DatabaseAdviceSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class DatabaseConnectionAdvice
{
    public DatabaseConnectionAdvice(string code, DatabaseAdviceSeverity severity, string message)
    {
        Code = code;
        Severity = severity;
        Message = message;
    }

    public string Code { get; }
    public DatabaseAdviceSeverity Severity { get; }
    public string Message { get; }
}

/// <summary>Provider-neutral, non-blocking connection-string diagnostics.</summary>
public static class ConnectionIntelligence
{
    public static IReadOnlyList<DatabaseConnectionAdvice> Analyze(string providerName, string? connectionString)
    {
        var result = new List<DatabaseConnectionAdvice>();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            result.Add(new DatabaseConnectionAdvice("connection.empty", DatabaseAdviceSeverity.Critical, "Connection string is empty."));
            return result;
        }

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            result.Add(new DatabaseConnectionAdvice("connection.parse", DatabaseAdviceSeverity.Warning, "Connection string could not be parsed by the provider-neutral parser."));
            return result;
        }

        var provider = providerName ?? string.Empty;
        if (provider.IndexOf("Postgre", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("Supabase", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("Cockroach", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AnalyzePostgres(builder, result);
        }
        else if (provider.IndexOf("SqlServer", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("SQL Server", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AnalyzeSqlServer(builder, result);
        }
        else if (provider.IndexOf("MySql", StringComparison.OrdinalIgnoreCase) >= 0
            || provider.IndexOf("Maria", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AnalyzeMySql(builder, result);
        }

        return result;
    }

    private static void AnalyzePostgres(DbConnectionStringBuilder builder, List<DatabaseConnectionAdvice> result)
    {
        var host = Get(builder, "Host", "Server");
        var port = Get(builder, "Port");
        var ssl = Get(builder, "SSL Mode", "SslMode", "SSLMode");

        if (string.Equals(ssl, "Disable", StringComparison.OrdinalIgnoreCase))
            result.Add(new DatabaseConnectionAdvice("postgres.ssl.disabled", DatabaseAdviceSeverity.Warning, "PostgreSQL SSL is disabled."));

        if (!string.IsNullOrEmpty(host) && host.IndexOf("pooler.supabase.com", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (port == "6543")
                result.Add(new DatabaseConnectionAdvice("supabase.pooler.transaction", DatabaseAdviceSeverity.Info, "Supabase transaction pooler detected; suitable for short-lived/serverless runtime traffic. Avoid session-dependent features and prepared statements."));
            else if (port == "5432")
                result.Add(new DatabaseConnectionAdvice("supabase.pooler.session", DatabaseAdviceSeverity.Info, "Supabase session pooler detected; suitable for persistent IPv4 application traffic."));
        }
    }

    private static void AnalyzeSqlServer(DbConnectionStringBuilder builder, List<DatabaseConnectionAdvice> result)
    {
        var encrypt = Get(builder, "Encrypt");
        if (string.Equals(encrypt, "False", StringComparison.OrdinalIgnoreCase) || string.Equals(encrypt, "No", StringComparison.OrdinalIgnoreCase))
            result.Add(new DatabaseConnectionAdvice("sqlserver.encrypt.disabled", DatabaseAdviceSeverity.Warning, "SQL Server encryption is explicitly disabled."));

        var trust = Get(builder, "Trust Server Certificate", "TrustServerCertificate");
        if (string.Equals(trust, "True", StringComparison.OrdinalIgnoreCase) || string.Equals(trust, "Yes", StringComparison.OrdinalIgnoreCase))
            result.Add(new DatabaseConnectionAdvice("sqlserver.trust-certificate", DatabaseAdviceSeverity.Info, "TrustServerCertificate is enabled; certificate identity is not validated."));
    }

    private static void AnalyzeMySql(DbConnectionStringBuilder builder, List<DatabaseConnectionAdvice> result)
    {
        var ssl = Get(builder, "SslMode", "SSL Mode");
        if (string.Equals(ssl, "None", StringComparison.OrdinalIgnoreCase) || string.Equals(ssl, "Disabled", StringComparison.OrdinalIgnoreCase))
            result.Add(new DatabaseConnectionAdvice("mysql.ssl.disabled", DatabaseAdviceSeverity.Warning, "MySQL/MariaDB TLS is disabled."));
    }

    private static string? Get(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
            if (builder.TryGetValue(key, out var value) && value != null)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }
}
