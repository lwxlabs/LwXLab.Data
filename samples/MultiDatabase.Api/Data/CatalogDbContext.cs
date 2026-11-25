using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MultiDatabase.Api.Models;

namespace MultiDatabase.Api.Data;

public sealed class CatalogDbContext : LwXDbContext<CatalogDbContext>
{
    public CatalogDbContext(
        DbContextOptions<CatalogDbContext> options,
        LwXDbContextDescriptor<CatalogDbContext> descriptor)
        : base(options, descriptor)
    {
    }

    public DbSet<AppRelease> Releases => Set<AppRelease>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppRelease>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
        });
    }
}
