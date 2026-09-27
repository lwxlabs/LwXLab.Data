using LwXLab.Data.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace LwXLab.Data.OpenTelemetry;

public static class LwXOpenTelemetryExtensions
{
    public static TracerProviderBuilder AddLwXDataInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(LwXDataTelemetry.ActivitySourceName);

    public static MeterProviderBuilder AddLwXDataInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(LwXDataTelemetry.MeterName);
}
