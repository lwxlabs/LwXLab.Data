using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LwXLab.Data;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LwXLab.Data.Diagnostics;

public enum DatabaseStartupMode
{
    None,
    Connectivity,
    Validate,
    Migrate,
    Strict,
    Development
}

public sealed class LwXDataStartupOptions
{
    public DatabaseStartupMode Mode { get; set; } = DatabaseStartupMode.None;
    public bool AllowAutomaticMigrations { get; set; }
    public bool AllowDestructiveMigrations { get; set; }
}

internal sealed class LwXDataStartupService : IHostedService
{
    private readonly ILwXDataDiagnostics _diagnostics;
    private readonly LwXDataDiagnosticsOptions _diagnosticsOptions;
    private readonly LwXDataStartupOptions _startup;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<ILwXDbContextDescriptor> _descriptors;
    private readonly ILogger<LwXDataStartupService> _logger;

    public LwXDataStartupService(
        ILwXDataDiagnostics diagnostics,
        LwXDataDiagnosticsOptions diagnosticsOptions,
        LwXDataStartupOptions startup,
        IServiceScopeFactory scopeFactory,
        IEnumerable<ILwXDbContextDescriptor> descriptors,
        ILogger<LwXDataStartupService> logger)
    {
        _diagnostics = diagnostics;
        _diagnosticsOptions = diagnosticsOptions;
        _startup = startup;
        _scopeFactory = scopeFactory;
        _descriptors = descriptors.ToList();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_startup.Mode == DatabaseStartupMode.None) return;

        IReadOnlyList<DatabaseDiagnosticReport> reports;
        if (_startup.Mode == DatabaseStartupMode.Connectivity)
            reports = await CheckConnectivityOnlyAsync(cancellationToken).ConfigureAwait(false);
        else
            reports = await _diagnostics.RunAsync(cancellationToken).ConfigureAwait(false);

        foreach (var report in reports)
            foreach (var check in report.Checks.Where(x => x.Status != DatabaseCheckStatus.Passed))
                _logger.LogWarning("Database {Database}: {Check} - {Status}: {Message}", report.Name, check.Check, check.Status, check.Message);

        var failures = reports.Any(x => x.HasFailures);
        if ((_startup.Mode == DatabaseStartupMode.Strict || _diagnosticsOptions.FailStartupOnDiagnosticError) && failures)
            throw new InvalidOperationException("LwXLab.Data strict startup validation failed. Review the database diagnostic logs.");

        if (_startup.Mode == DatabaseStartupMode.Migrate || _startup.Mode == DatabaseStartupMode.Development)
        {
            if (!_startup.AllowAutomaticMigrations)
            {
                _logger.LogWarning("Database startup mode requests migrations, but AllowAutomaticMigrations=false. No migrations were executed.");
                return;
            }

            foreach (var descriptor in _descriptors)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetService(descriptor.ContextType) as DbContext;
                    if (context == null) continue;
                    var report = await MigrationSafetyAnalyzer.AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
                    if (report.PendingMigrations.Count == 0) continue;
                    if (report.HighestRisk == MigrationRisk.High && !_startup.AllowDestructiveMigrations)
                        throw new InvalidOperationException("Automatic migration for '" + descriptor.Name + "' was blocked because destructive migration SQL was detected. Set AllowDestructiveMigrations=true only after explicit review.");
                    await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Applied {Count} migration(s) for database {Database}.", report.PendingMigrations.Count, descriptor.Name);
                }
            }
        }
    }

    private async Task<IReadOnlyList<DatabaseDiagnosticReport>> CheckConnectivityOnlyAsync(CancellationToken cancellationToken)
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
                    var canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                    report.Checks.Add(new DatabaseCheckResult("connectivity", canConnect ? DatabaseCheckStatus.Passed : DatabaseCheckStatus.Failed, canConnect ? "Connection succeeded." : "Connection failed."));
                }
                catch (Exception ex)
                {
                    report.Checks.Add(new DatabaseCheckResult("connectivity", DatabaseCheckStatus.Failed, ex.GetType().Name + ": " + ex.Message));
                }
            }
        }
        return reports;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
