using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LwXLab.Data.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LwXLab.Data.AspNetCore;

internal sealed class LwXDataHealthCheck : IHealthCheck
{
    private readonly ILwXDataDiagnostics _diagnostics;
    public LwXDataHealthCheck(ILwXDataDiagnostics diagnostics) { _diagnostics = diagnostics; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var reports = await _diagnostics.RunAsync(cancellationToken).ConfigureAwait(false);
        if (reports.Any(x => x.HasFailures)) return HealthCheckResult.Unhealthy("One or more database checks failed.");
        if (reports.SelectMany(x => x.Checks).Any(x => x.Status == DatabaseCheckStatus.Warning)) return HealthCheckResult.Degraded("Database checks contain warnings.");
        return HealthCheckResult.Healthy("Database checks passed.");
    }
}
