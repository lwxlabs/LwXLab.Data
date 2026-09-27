using System;
using LwXLab.Data.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace LwXLab.Data.AspNetCore;

public static class LwXDataAspNetCoreExtensions
{
    public static IHealthChecksBuilder AddLwXDataHealthCheck(this IServiceCollection services, string name = "lwxlabs-data")
    {
        services.AddLogging();
        if (!services.Any(d => d.ServiceType == typeof(ILwXDataDiagnostics)))
            services.AddLwXDataDiagnostics();
        return services.AddHealthChecks().AddCheck<LwXDataHealthCheck>(name);
    }

    public static IEndpointConventionBuilder MapLwXDataHealth(this IEndpointRouteBuilder endpoints, string pattern = "/health/data")
        => endpoints.MapHealthChecks(pattern, new HealthCheckOptions());

    /// <summary>Starts an AsyncLocal query budget for each HTTP request. Observation only unless ThrowWhenQueryBudgetExceeded is explicitly enabled.</summary>
    public static IApplicationBuilder UseLwXDataQueryBudget(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var budget = context.RequestServices.GetRequiredService<IQueryBudgetAccessor>();
            using (budget.BeginScope(context.Request.Method + " " + context.Request.Path))
                await next().ConfigureAwait(false);
        });
    }
}
