using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MultiDatabase.Api.Models;

namespace MultiDatabase.Api.Data;

public sealed class InternalDbContext : LwXDbContext<InternalDbContext>
{
    public InternalDbContext(
        DbContextOptions<InternalDbContext> options,
        LwXDbContextDescriptor<InternalDbContext> descriptor)
        : base(options, descriptor)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
}
