using System;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace LwXLab.Data.Diagnostics;

public static class LwXDataDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Adds opt-in diagnostics. Safe defaults report problems only; no destructive command is blocked,
    /// no migration runs automatically, startup does not fail, and query budgets do not throw.
    /// </summary>
    public static IServiceCollection AddLwXDataDiagnostics(this IServiceCollection services, Action<LwXDataDiagnosticsOptions>? configure = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        var options = new LwXDataDiagnosticsOptions();
        configure?.Invoke(options);
        options.Validate();
        services.AddLogging();

        // Replace intentionally: an explicit later registration should be able to configure the feature set.
        services.RemoveAll<LwXDataDiagnosticsOptions>();
        services.AddSingleton(options);
        services.TryAddSingleton<IQueryBudgetAccessor, QueryBudgetAccessor>();
        services.TryAddSingleton<LwXDataCommandInterceptor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILwXDbContextInterceptorProvider, LwXDiagnosticsInterceptorProvider>());
        services.TryAddSingleton<ILwXDataDiagnostics, LwXDataDiagnosticsService>();
        return services;
    }

    /// <summary>Enables optional startup validation. Default mode is None, so merely registering this is non-blocking.</summary>
    public static IServiceCollection AddLwXDataStartupChecks(this IServiceCollection services, Action<LwXDataStartupOptions>? configure = null)
    {
        if (!services.Any(d => d.ServiceType == typeof(ILwXDataDiagnostics)))
            services.AddLwXDataDiagnostics();
        var options = new LwXDataStartupOptions();
        configure?.Invoke(options);
        services.RemoveAll<LwXDataStartupOptions>();
        services.AddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, LwXDataStartupService>());
        return services;
    }
}
