using System;
using System.Text.RegularExpressions;

namespace LwXLab.Data;

/// <summary>Validates portable database identifiers used for schemas and migration tables.</summary>
public static class DatabaseIdentifier
{
    private static readonly Regex SafeIdentifier = new Regex(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsValid(string? value, int maxLength = 128)
    {
        return maxLength > 0
            && !string.IsNullOrWhiteSpace(value)
            && value!.Length <= maxLength
            && SafeIdentifier.IsMatch(value);
    }

    public static string Validate(string? value, string? parameterName = null, int maxLength = 128)
    {
        if (!IsValid(value, maxLength))
        {
            throw new ArgumentException(
                $"Database identifiers must be 1-{maxLength} ASCII characters, start with a letter or underscore, and contain only letters, digits, or underscores.",
                parameterName ?? nameof(value));
        }

        return value!;
    }

    /// <summary>
    /// Validates an identifier against PostgreSQL's 63-byte identifier limit. ASCII-only validation makes the byte and character limits equivalent here.
    /// </summary>
    public static string ValidatePostgreSql(string? value, string? parameterName = null)
        => Validate(value, parameterName, 63);
}
