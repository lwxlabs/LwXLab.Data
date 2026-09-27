using System;
using LwXLab.Data;

namespace LwXLab.Data.Diagnostics;

/// <summary>
/// Opt-in diagnostics and safety settings. Defaults intentionally observe/report only;
/// they never migrate, delete, block startup, or throw on query budgets.
/// </summary>
public sealed class LwXDataDiagnosticsOptions
{
    public bool CheckConnectivity { get; set; } = true;
    public bool CheckMigrations { get; set; } = true;
    public bool CheckSchema { get; set; } = true;
    public bool CheckPermissions { get; set; }
    public bool CheckDrift { get; set; }
    public bool EnableConnectionIntelligence { get; set; } = true;

    public bool EnableMigrationSafety { get; set; }
    public bool EnableEnvironmentProtection { get; set; }
    public DatabaseEnvironment Environment { get; set; } = DatabaseEnvironment.Unknown;
    public bool BlockDestructiveCommandsInProduction { get; set; }

    public bool EnableQueryDiagnostics { get; set; }
    public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromMilliseconds(750);
    public int RepeatedQueryWarningCount { get; set; } = 10;
    public int LargeResultSetWarningRows { get; set; } = 1000;
    public bool WarnOnPotentiallyUnboundedQueries { get; set; } = true;

    public bool EnableQueryBudget { get; set; }
    public int QueryBudgetWarningCount { get; set; } = 25;
    public int QueryBudgetMaximumCount { get; set; } = 100;
    public bool ThrowWhenQueryBudgetExceeded { get; set; }

    public bool FailStartupOnDiagnosticError { get; set; }
    public bool IncludeSqlTextInLogs { get; set; }


    /// <summary>Restores the deliberately beginner-safe, observation-only behavior.</summary>
    public LwXDataDiagnosticsOptions UseBeginnerSafeDefaults()
    {
        CheckConnectivity = true;
        CheckMigrations = true;
        CheckSchema = true;
        CheckPermissions = false;
        CheckDrift = false;
        EnableConnectionIntelligence = true;
        EnableMigrationSafety = false;
        EnableEnvironmentProtection = false;
        BlockDestructiveCommandsInProduction = false;
        EnableQueryDiagnostics = false;
        EnableQueryBudget = false;
        ThrowWhenQueryBudgetExceeded = false;
        FailStartupOnDiagnosticError = false;
        IncludeSqlTextInLogs = false;
        Environment = DatabaseEnvironment.Unknown;
        return this;
    }

    /// <summary>Turns off every setting in this options type that can reject an operation or fail startup.</summary>
    public LwXDataDiagnosticsOptions DisableBlockingBehavior()
    {
        EnableEnvironmentProtection = false;
        BlockDestructiveCommandsInProduction = false;
        ThrowWhenQueryBudgetExceeded = false;
        FailStartupOnDiagnosticError = false;
        return this;
    }

    public void Validate()
    {
        if (SlowQueryThreshold < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(SlowQueryThreshold));
        if (RepeatedQueryWarningCount < 1) throw new ArgumentOutOfRangeException(nameof(RepeatedQueryWarningCount));
        if (LargeResultSetWarningRows < 1) throw new ArgumentOutOfRangeException(nameof(LargeResultSetWarningRows));
        if (QueryBudgetWarningCount < 1) throw new ArgumentOutOfRangeException(nameof(QueryBudgetWarningCount));
        if (QueryBudgetMaximumCount < QueryBudgetWarningCount) throw new ArgumentException("QueryBudgetMaximumCount must be >= QueryBudgetWarningCount.");
    }
}
