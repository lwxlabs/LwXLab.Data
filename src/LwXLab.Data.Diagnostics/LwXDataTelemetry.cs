using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LwXLab.Data.Diagnostics;

/// <summary>Built-in Activity/Meter sources. No exporter or OpenTelemetry dependency is required.</summary>
public static class LwXDataTelemetry
{
    public const string ActivitySourceName = "LwXLab.Data";
    public const string MeterName = "LwXLab.Data";
    public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);
    internal static readonly Meter Meter = new Meter(MeterName);
    internal static readonly Counter<long> QueryCounter = Meter.CreateCounter<long>("lwxlabs.data.query.count");
    internal static readonly Counter<long> QueryFailureCounter = Meter.CreateCounter<long>("lwxlabs.data.query.failures");
    internal static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>("lwxlabs.data.query.duration.ms", "ms");
    internal static readonly Counter<long> BudgetExceededCounter = Meter.CreateCounter<long>("lwxlabs.data.query_budget.exceeded");
}
