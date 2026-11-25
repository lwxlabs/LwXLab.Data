using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void SupabaseRegistration_StoresProviderAndSchemaPerContext()
    {
        var services = new ServiceCollection();
        services.AddSupabaseDbContext<SchemaDbContext>(
            "Host=aws-0-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.project;Password=secret;SSL Mode=Require",
            "catalog");

        using var provider = services.BuildServiceProvider();
        var descriptor = provider.GetRequiredService<LwXDbContextDescriptor<SchemaDbContext>>();

        Assert.Equal(DatabaseProviderNames.Supabase, descriptor.ProviderName);
        Assert.Equal("catalog", descriptor.Schema);
        Assert.Equal("__EFMigrationsHistory", descriptor.MigrationsHistoryTable);
    }

    [Fact]
    public void SqliteRegistration_DoesNotRequireSchema()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<NoSchemaDbContext>("Data Source=:memory:");

        using var provider = services.BuildServiceProvider();
        var descriptor = provider.GetRequiredService<LwXDbContextDescriptor<NoSchemaDbContext>>();

        Assert.Equal(DatabaseProviderNames.Sqlite, descriptor.ProviderName);
        Assert.Null(descriptor.Schema);
    }

    [Fact]
    public void MySqlRegistration_DoesNotRequireSchema()
    {
        var services = new ServiceCollection();
        services.AddMySqlDbContext<NoSchemaDbContext>(
            "Server=localhost;Database=catalog;User Id=user;Password=secret;SslMode=Required");

        using var provider = services.BuildServiceProvider();
        var descriptor = provider.GetRequiredService<LwXDbContextDescriptor<NoSchemaDbContext>>();

        Assert.Equal(DatabaseProviderNames.MySql, descriptor.ProviderName);
        Assert.Null(descriptor.Schema);
    }

    [Fact]
    public void CockroachDbRegistration_UsesSchema()
    {
        var services = new ServiceCollection();
        services.AddCockroachDbDbContext<SchemaDbContext>(
            "Host=localhost;Database=defaultdb;Username=root;Password=secret;SSL Mode=Require",
            "catalog");

        using var provider = services.BuildServiceProvider();
        var descriptor = provider.GetRequiredService<LwXDbContextDescriptor<SchemaDbContext>>();

        Assert.Equal(DatabaseProviderNames.CockroachDb, descriptor.ProviderName);
        Assert.Equal("catalog", descriptor.Schema);
    }

    private sealed class SchemaDbContext : LwXDbContext<SchemaDbContext>
    {
        public SchemaDbContext(
            DbContextOptions<SchemaDbContext> options,
            LwXDbContextDescriptor<SchemaDbContext> descriptor)
            : base(options, descriptor)
        {
        }
    }

    private sealed class NoSchemaDbContext : LwXDbContext<NoSchemaDbContext>
    {
        public NoSchemaDbContext(
            DbContextOptions<NoSchemaDbContext> options,
            LwXDbContextDescriptor<NoSchemaDbContext> descriptor)
            : base(options, descriptor)
        {
        }
    }
}
