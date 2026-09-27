using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace LwXLab.Data;

/// <summary>Redacts common secrets from database connection strings without requiring a provider package.</summary>
public static class ConnectionStringRedactor
{
    private static readonly HashSet<string> SecretKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Pwd", "Pass", "Secret", "Access Token", "AccessToken", "Token", "ApiKey", "API Key"
    };

    public static string Redact(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var keys = new List<string>();
            foreach (string key in builder.Keys)
                keys.Add(key);

            foreach (var key in keys)
            {
                if (IsSecretKey(key))
                    builder[key] = "********";
            }

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            // A malformed quoted value can contain semicolons. Never echo fragments
            // when parsing fails, because they may be part of a secret.
            return "[invalid connection string redacted]";
        }
    }

    private static bool IsSecretKey(string key)
    {
        if (SecretKeys.Contains(key))
            return true;

        return key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0
            || key.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
