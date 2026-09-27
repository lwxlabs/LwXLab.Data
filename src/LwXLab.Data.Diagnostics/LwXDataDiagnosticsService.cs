using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data.Diagnostics;

public interface ILwXDataDiagnostics
{
    Task<IReadOnlyList<DatabaseDiagnosticReport>> RunAsync(CancellationToken cancellationToken = default);
}

internal sealed class LwXDataDiagnosticsService : ILwXDataDiagnostics
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<ILwXDbContextDescriptor> _descriptors;
    private readonly LwXDataDiagnosticsOptions _options;

    public LwXDataDiagnosticsService(IServiceScopeFactory scopeFactory, IEnumerable<ILwXDbContextDescriptor> descriptors, LwXDataDiagnosticsOptions options)
    {
        _scopeFactory = scopeFactory; _descriptors = descriptors.ToList(); _options = options;
    }

    public async Task<IReadOnlyList<DatabaseDiagnosticReport>> RunAsync(CancellationToken cancellationToken = default)
    {
        var reports = new List<DatabaseDiagnosticReport>();
        foreach (var descriptor in _descriptors)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetService(descriptor.ContextType) as DbContext;
                var report = new DatabaseDiagnosticReport(descriptor.Name, descriptor.ProviderName, descriptor.Schema);
                reports.Add(report);
                if (context == null)
                {
                    report.Checks.Add(new DatabaseCheckResult("resolution", DatabaseCheckStatus.Failed, "DbContext could not be resolved from DI."));
                    continue;
                }

                try
                {
                    if (_options.CheckConnectivity)
                    {
                        var canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                        report.Checks.Add(new DatabaseCheckResult("connectivity", canConnect ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Failed, canConnect ? "Connection succeeded." : "Connection failed."));
                        if (!canConnect) continue;
                    }

                    var connection = context.Database.GetDbConnection();
                    report.ServerVersion = await DbConnectionMetadata.ReadServerVersionAsync(connection, cancellationToken).ConfigureAwait(false);

                    if (_options.CheckMigrations)
                    {
                        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
                        report.Checks.Add(new DatabaseCheckResult("migrations", pending.Count == 0 ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Warning,
                            pending.Count == 0 ? "Migrations are current." : pending.Count + " pending migration(s): " + string.Join(", ", pending)));
                    }

                    if (_options.CheckSchema && descriptor.Schema != null)
                    {
                        var exists = await SchemaInspector.ExistsAsync(connection, descriptor.ProviderName, descriptor.Schema, cancellationToken).ConfigureAwait(false);
                        report.Checks.Add(exists == null
                            ? new DatabaseCheckResult("schema", DatabaseCheckStatus.Skipped, "Provider does not use or expose independently checkable schemas.")
                            : new DatabaseCheckResult("schema", exists.Value ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Failed, exists.Value ? "Configured schema exists." : "Configured schema was not found."));
                    }

                    if (_options.CheckPermissions && descriptor.Schema != null)
                    {
                        var isolation = await SchemaIsolationChecker.CheckAsync(connection, descriptor.ProviderName, descriptor.Schema, cancellationToken).ConfigureAwait(false);
                        var status = !isolation.Supported ? DatabaseCheckStatus.Skipped
                            : isolation.CanUseSchema ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Warning;
                        report.Checks.Add(new DatabaseCheckResult("permissions", status, isolation.Message + (isolation.Supported ? " Use=" + isolation.CanUseSchema + ", Create=" + isolation.CanCreateInSchema + "." : string.Empty)));
                    }

                    if (_options.EnableMigrationSafety)
                    {
                        var migrationSafety = await MigrationSafetyAnalyzer.AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
                        var status = migrationSafety.HighestRisk == MigrationRisk.High || migrationSafety.HighestRisk == MigrationRisk.Medium
                            ? DatabaseCheckStatus.Warning : DatabaseCheckStatus.Passed;
                        var message = migrationSafety.PendingMigrations.Count == 0
                            ? "No pending migrations."
                            : migrationSafety.PendingMigrations.Count + " pending migration(s); highest detected risk: " + migrationSafety.HighestRisk + ".";
                        report.Checks.Add(new DatabaseCheckResult("migration-safety", status, message));
                    }

                    if (_options.CheckDrift)
                    {
                        var model = DatabaseDriftDetector.FromEfModel(context, descriptor.Schema);
                        var live = await DatabaseDriftDetector.FromDatabaseAsync(connection, descriptor.ProviderName, descriptor.Schema, cancellationToken).ConfigureAwait(false);
                        live = DatabaseDriftDetector.ExcludeTable(live, descriptor.Schema, descriptor.MigrationsHistoryTable);
                        var diff = DatabaseDriftDetector.Diff(model, live);
                        report.Checks.Add(new DatabaseCheckResult("drift", diff.Count == 0 ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Warning,
                            diff.Count == 0 ? "EF model and live schema fingerprints match." : diff.Count + " schema difference(s) detected."));
                    }

                    if (_options.EnableConnectionIntelligence)
                    {
                        var advice = ConnectionIntelligence.Analyze(descriptor.ProviderName, context.Database.GetConnectionString());
                        foreach (var item in advice)
                        {
                            var status = item.Severity == DatabaseAdviceSeverity.Critical ? DatabaseCheckStatus.Failed
                                : item.Severity == DatabaseAdviceSeverity.Warning ? DatabaseCheckStatus.Warning : DatabaseCheckStatus.Passed;
                            report.Checks.Add(new DatabaseCheckResult("connection:" + item.Code, status, item.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.Checks.Add(new DatabaseCheckResult("diagnostics", DatabaseCheckStatus.Failed, ex.GetType().Name + ": " + ex.Message));
                }
            }
        }
        return reports;
    }
}
