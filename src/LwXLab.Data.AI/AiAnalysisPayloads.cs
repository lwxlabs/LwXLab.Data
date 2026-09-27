using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LwXLab.Data.Diagnostics;

namespace LwXLab.Data.AI;

public sealed class AiMigrationReview
{
    public string Risk { get; set; } = string.Empty;
    public List<string> PendingMigrations { get; set; } = new List<string>();
    public List<string> Findings { get; set; } = new List<string>();
    public string ToJson(bool indented = true) => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = indented });

    public static AiMigrationReview From(MigrationSafetyReport report) => new AiMigrationReview
    {
        Risk = report.HighestRisk.ToString(),
        PendingMigrations = report.PendingMigrations.ToList(),
        Findings = report.Findings.Select(x => x.Code + ": " + x.Message).ToList()
    };
}

public sealed class AiQueryAnalysisPayload
{
    public string Provider { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string? ExecutionPlan { get; set; }
    public double? DurationMilliseconds { get; set; }
    public List<string> KnownIndexes { get; set; } = new List<string>();
    public List<string> DeterministicHints { get; set; } = new List<string>();
    public string ToJson(bool indented = true) => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = indented });
}

public static class AiQueryAnalysis
{
    /// <summary>Creates an explicit payload for an AI tool. The caller controls whether SQL/plan text leaves the process.</summary>
    public static AiQueryAnalysisPayload Create(string provider, string sql, string? executionPlan = null, TimeSpan? duration = null, IEnumerable<string>? knownIndexes = null)
        => new AiQueryAnalysisPayload
        {
            Provider = provider ?? string.Empty,
            Sql = sql ?? string.Empty,
            ExecutionPlan = executionPlan,
            DurationMilliseconds = duration?.TotalMilliseconds,
            KnownIndexes = knownIndexes == null ? new List<string>() : knownIndexes.ToList(),
            DeterministicHints = Analyze(sql ?? string.Empty, executionPlan, duration)
        };

    private static List<string> Analyze(string sql, string? executionPlan, TimeSpan? duration)
    {
        var hints = new List<string>();
        var normalized = (sql ?? string.Empty).Trim();
        if (normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && normalized.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) < 0)
            hints.Add("SELECT has no WHERE clause; verify that an unbounded scan is intentional.");
        if (normalized.IndexOf("SELECT *", StringComparison.OrdinalIgnoreCase) >= 0)
            hints.Add("SELECT * detected; selecting only required columns can reduce transfer and materialization cost.");
        if (executionPlan != null && executionPlan.IndexOf("Seq Scan", StringComparison.OrdinalIgnoreCase) >= 0)
            hints.Add("Execution plan contains a sequential scan; review selectivity and indexes.");
        if (executionPlan != null && executionPlan.IndexOf("Table Scan", StringComparison.OrdinalIgnoreCase) >= 0)
            hints.Add("Execution plan contains a table scan; review selectivity and indexes.");
        if (duration.HasValue && duration.Value > TimeSpan.FromSeconds(1))
            hints.Add("Observed duration exceeds one second.");
        return hints;
    }
}
