using System;

namespace LwXLab.Data.Oracle;

/// <summary>Connection and pooling options for Oracle ODP.NET Core.</summary>
public sealed class OracleDatabaseOptions
{
    public OracleDatabaseOptions()
    {
    }

    public OracleDatabaseOptions(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; } = string.Empty;
    public int? ConnectionTimeoutSeconds { get; set; }
    public int? MaximumPoolSize { get; set; }
    public int? MinimumPoolSize { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("An Oracle connection string is required.", nameof(ConnectionString));
        if (ConnectionTimeoutSeconds.HasValue && ConnectionTimeoutSeconds.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(ConnectionTimeoutSeconds), "Connection timeout must be greater than zero.");
        if (MaximumPoolSize.HasValue && MaximumPoolSize.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumPoolSize), "Maximum pool size must be greater than zero.");
        if (MinimumPoolSize.HasValue && MinimumPoolSize.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumPoolSize), "Minimum pool size cannot be negative.");
        if (MaximumPoolSize.HasValue && MinimumPoolSize.HasValue && MinimumPoolSize.Value > MaximumPoolSize.Value)
            throw new ArgumentException("MinimumPoolSize cannot be greater than MaximumPoolSize.");
    }
}
