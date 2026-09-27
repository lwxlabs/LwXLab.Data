using System;

namespace LwXLab.Data.PostgreSql;

/// <summary>Connection and resilience options for PostgreSQL/Npgsql.</summary>
public class PostgreSqlDatabaseOptions
{
    public PostgreSqlDatabaseOptions()
    {
    }

    public PostgreSqlDatabaseOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; } = string.Empty;
    public bool RequireSsl { get; set; } = true;
    public string? ApplicationName { get; set; }
    public int? CommandTimeoutSeconds { get; set; }
    public int? ConnectTimeoutSeconds { get; set; }
    public int? MaximumPoolSize { get; set; }
    public int? MinimumPoolSize { get; set; }
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 5;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(ConnectionString));
        if (CommandTimeoutSeconds.HasValue && CommandTimeoutSeconds.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(CommandTimeoutSeconds), "Command timeout must be greater than zero.");
        if (ConnectTimeoutSeconds.HasValue && ConnectTimeoutSeconds.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeoutSeconds), "Connect timeout must be greater than zero.");
        if (MaximumPoolSize.HasValue && MaximumPoolSize.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumPoolSize), "Maximum pool size must be greater than zero.");
        if (MinimumPoolSize.HasValue && MinimumPoolSize.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumPoolSize), "Minimum pool size cannot be negative.");
        if (MaximumPoolSize.HasValue && MinimumPoolSize.HasValue && MinimumPoolSize.Value > MaximumPoolSize.Value)
            throw new ArgumentException("MinimumPoolSize cannot be greater than MaximumPoolSize.");
        if (MaxRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetryCount), "Retry count cannot be negative.");
        if (MaxRetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaxRetryDelay), "Retry delay cannot be negative.");
    }
}
