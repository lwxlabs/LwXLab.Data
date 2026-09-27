using System;
using System.Collections.Generic;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Data;

public static class LwXDbContextServiceCollectionExtensions
{
    public static IServiceCollection AddLwXDbContext<TContext>(
        this IServiceCollection services,
        Action<LwXDbContextBuilder<TContext>> configure,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var registration = new LwXDbContextBuilder<TContext>(typeof(TContext).Name);
        configure(registration);
        registration.Validate();
        var descriptor = registration.BuildDescriptor();

        services.AddSingleton(descriptor);
        services.AddSingleton<ILwXDbContextDescriptor>(descriptor);
        services.AddDbContext<TContext>(
            (serviceProvider, options) =>
            {
                registration.ConfigureProvider(options);
                AttachOptionalInterceptors(serviceProvider, descriptor, options);
            },
            contextLifetime,
            optionsLifetime);

        return services;
    }

    public static IServiceCollection AddLwXDbContextFactory<TContext>(
        this IServiceCollection services,
        Action<LwXDbContextBuilder<TContext>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContext : DbContext
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var registration = new LwXDbContextBuilder<TContext>(typeof(TContext).Name);
        configure(registration);
        registration.Validate();
        var descriptor = registration.BuildDescriptor();

        services.AddSingleton(descriptor);
        services.AddSingleton<ILwXDbContextDescriptor>(descriptor);
        services.AddDbContextFactory<TContext>(
            (serviceProvider, options) =>
            {
                registration.ConfigureProvider(options);
                AttachOptionalInterceptors(serviceProvider, descriptor, options);
            },
            lifetime);

        return services;
    }

    private static void AttachOptionalInterceptors(
        IServiceProvider serviceProvider,
        ILwXDbContextDescriptor descriptor,
        DbContextOptionsBuilder options)
    {
        var providers = serviceProvider.GetServices<ILwXDbContextInterceptorProvider>();
        var interceptors = new List<IInterceptor>();
        foreach (var provider in providers)
            interceptors.AddRange(provider.GetInterceptors(descriptor));

        if (interceptors.Count > 0)
            options.AddInterceptors(interceptors);
    }
}
