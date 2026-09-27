using System;
using Microsoft.EntityFrameworkCore;

namespace LwXLab.Data.EntityFrameworkCore;

/// <summary>
/// Optional base DbContext that automatically applies a configured default schema
/// for providers that support schemas.
/// </summary>
public abstract class LwXDbContext<TContext> : DbContext
    where TContext : DbContext
{
    private readonly LwXDbContextDescriptor<TContext> _descriptor;

    protected LwXDbContext(
        DbContextOptions<TContext> options,
        LwXDbContextDescriptor<TContext> descriptor)
        : base(options)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public string DatabaseName => _descriptor.Name;
    public string DatabaseProviderName => _descriptor.ProviderName;
    public string? DatabaseSchema => _descriptor.Schema;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (_descriptor.Schema != null)
            modelBuilder.HasDefaultSchema(_descriptor.Schema);

        ConfigureModel(modelBuilder);
    }

    /// <summary>Override this method for application-specific EF model configuration.</summary>
    protected virtual void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}
