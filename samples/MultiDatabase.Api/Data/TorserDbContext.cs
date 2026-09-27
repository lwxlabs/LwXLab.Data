using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MultiDatabase.Api.Models;

namespace MultiDatabase.Api.Data;

public sealed class TorserDbContext : LwXDbContext<TorserDbContext>
{
    public TorserDbContext(
        DbContextOptions<TorserDbContext> options,
        LwXDbContextDescriptor<TorserDbContext> descriptor)
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
