using System;
using System.Threading;
using System.Threading.Tasks;
using LwXLab.Data;
using Microsoft.EntityFrameworkCore;

namespace LwXLab.Data.Diagnostics;

public static class SafeDatabaseOperations
{
    public static async Task<MigrationSafetyReport> MigrateSafelyAsync(
        this DbContext context,
        DatabaseEnvironment environment,
        bool allowDestructiveMigrations = false,
        CancellationToken cancellationToken = default)
    {
        var report = await MigrationSafetyAnalyzer.AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
        if (report.PendingMigrations.Count == 0) return report;
        if (environment == DatabaseEnvironment.Production && report.HighestRisk == MigrationRisk.High && !allowDestructiveMigrations)
            throw new InvalidOperationException("Destructive migration detected in Production. Pass allowDestructiveMigrations:true only after explicit review.");
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        return report;
    }

    public static async Task<bool> EnsureDeletedSafelyAsync(this DbContext context, DatabaseEnvironment environment, bool allowProductionDelete = false, CancellationToken cancellationToken = default)
    {
        if (environment == DatabaseEnvironment.Production && !allowProductionDelete)
            throw new InvalidOperationException("EnsureDeleted is blocked for Production by LwXLab.Data safe operations.");
        return await context.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
    }
}
