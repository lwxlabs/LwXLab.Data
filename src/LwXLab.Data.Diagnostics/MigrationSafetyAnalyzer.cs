using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LwXLab.Data.Diagnostics;

public enum MigrationRisk
{
    None,
    Low,
    Medium,
    High
}

public sealed class MigrationSafetyFinding
{
    public MigrationSafetyFinding(MigrationRisk risk, string code, string message)
    {
        Risk = risk; Code = code; Message = message;
    }
    public MigrationRisk Risk { get; }
    public string Code { get; }
    public string Message { get; }
}

public sealed class MigrationSafetyReport
{
    public List<string> PendingMigrations { get; } = new List<string>();
    public List<MigrationSafetyFinding> Findings { get; } = new List<MigrationSafetyFinding>();
    public MigrationRisk HighestRisk => Findings.Count == 0 ? MigrationRisk.None : Findings.Max(x => x.Risk);
    public string? Script { get; internal set; }
}

public static class MigrationSafetyAnalyzer
{
    private static readonly Regex DropTable = new Regex(@"\bDROP\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropColumn = new Regex(@"\bDROP\s+COLUMN\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Truncate = new Regex(@"\bTRUNCATE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropDatabase = new Regex(@"\bDROP\s+DATABASE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AlterColumn = new Regex(@"\bALTER\s+(?:COLUMN|TABLE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<MigrationSafetyReport> AnalyzeAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var report = new MigrationSafetyReport();
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        report.PendingMigrations.AddRange(pending);
        if (pending.Count == 0) return report;

        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var from = applied.Count == 0 ? null : applied[applied.Count - 1];
        var migrator = context.GetService<IMigrator>();
        var script = migrator.GenerateScript(from, null, MigrationsSqlGenerationOptions.Default);
        report.Script = script;

        if (DropDatabase.IsMatch(script)) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.High, "migration.drop_database", "Migration script drops a database."));
        if (DropTable.IsMatch(script)) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.High, "migration.drop_table", "Migration script drops one or more tables."));
        if (DropColumn.IsMatch(script)) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.High, "migration.drop_column", "Migration script drops one or more columns."));
        if (Truncate.IsMatch(script)) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.High, "migration.truncate", "Migration script truncates data."));
        if (AlterColumn.IsMatch(script)) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.Medium, "migration.alter", "Migration changes table/column definitions; review narrowing/type changes."));
        if (report.Findings.Count == 0) report.Findings.Add(new MigrationSafetyFinding(MigrationRisk.Low, "migration.pending", "Pending migrations were detected and no obvious destructive SQL pattern was found."));
        return report;
    }
}
