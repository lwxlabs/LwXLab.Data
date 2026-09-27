using System;
using System.Collections.Generic;

namespace LwXLab.Data.Diagnostics;

public enum DatabaseCheckStatus
{
    Passed,
    Warning,
    Failed,
    Skipped
}

public sealed class DatabaseCheckResult
{
    public DatabaseCheckResult(string check, DatabaseCheckStatus status, string message)
    {
        Check = check;
        Status = status;
        Message = message;
    }
    public string Check { get; }
    public DatabaseCheckStatus Status { get; }
    public string Message { get; }
}

public sealed class DatabaseDiagnosticReport
{
    public DatabaseDiagnosticReport(string name, string provider, string? schema)
    {
        Name = name;
        Provider = provider;
        Schema = schema;
    }
    public string Name { get; }
    public string Provider { get; }
    public string? Schema { get; }
    public string? ServerVersion { get; internal set; }
    public List<DatabaseCheckResult> Checks { get; } = new List<DatabaseCheckResult>();
    public bool HasFailures => Checks.Exists(x => x.Status == DatabaseCheckStatus.Failed);
}
