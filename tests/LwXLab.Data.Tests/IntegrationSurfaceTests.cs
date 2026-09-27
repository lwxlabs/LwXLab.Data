using System.Linq;
using System.Threading.Tasks;
using LwXLab.Data.AI;
using LwXLab.Data.AspNetCore;
using LwXLab.Data.Diagnostics;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.OpenTelemetry;
using LwXLab.Data.Tooling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class IntegrationSurfaceTests
{
    [Fact]
    public void AllSchemaAwareProviderRegistrations_StoreExpectedDescriptors()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlDbContext<PostgreSqlContext>(
            "Host=localhost;Database=test;Username=user;Password=secret;SSL Mode=Require", "app");
        services.AddSqlServerDbContext<SqlServerContext>(
            "Server=localhost;Database=test;User Id=user;Password=secret;Encrypt=True;TrustServerCertificate=False", "app");
        services.AddOracleDbContext<OracleContext>(
            "User Id=user;Password=secret;Data Source=localhost/XEPDB1", "APP");

        using var provider = services.BuildServiceProvider();
        Assert.Equal(DatabaseProviderNames.PostgreSql, provider.GetRequiredService<LwXDbContextDescriptor<PostgreSqlContext>>().ProviderName);
        Assert.Equal(DatabaseProviderNames.SqlServer, provider.GetRequiredService<LwXDbContextDescriptor<SqlServerContext>>().ProviderName);
        Assert.Equal(DatabaseProviderNames.Oracle, provider.GetRequiredService<LwXDbContextDescriptor<OracleContext>>().ProviderName);
    }

    [Fact]
    public async Task SqliteDiagnostics_CanReadServerVersionWithoutLeavingConnectionOpen()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<ModelContext>("Data Source=:memory:");
        services.AddLwXDataDiagnostics(options =>
        {
            options.CheckMigrations = false;
            options.CheckSchema = false;
            options.CheckPermissions = false;
            options.CheckDrift = false;
            options.EnableConnectionIntelligence = false;
        });

        using var provider = services.BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<ILwXDataDiagnostics>();
        var reports = await diagnostics.RunAsync();

        var report = Assert.Single(reports);
        Assert.False(string.IsNullOrWhiteSpace(report.ServerVersion));
        Assert.Contains(report.Checks, x => x.Check == "connectivity" && x.Status == DatabaseCheckStatus.Passed);
        Assert.False(report.HasFailures);
    }

    [Fact]
    public void AiSchemaContext_ContainsMetadataButNoConnectionString()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<ModelContext>("Data Source=:memory:");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var payload = AiSchemaContextFactory.Create(context, DatabaseProviderNames.Sqlite).ToJson();

        Assert.Contains(nameof(TestEntity), payload);
        Assert.DoesNotContain("Data Source", payload);
    }

    [Fact]
    public void AspNetCoreHealthCheck_AlsoRegistersDiagnostics()
    {
        var services = new ServiceCollection();
        services.AddLwXDataHealthCheck();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ILwXDataDiagnostics>());
    }

    [Fact]
    public void OpenTelemetryInstrumentation_BuildsTracerAndMeterProviders()
    {
        using var tracer = Sdk.CreateTracerProviderBuilder().AddLwXDataInstrumentation().Build();
        using var meter = Sdk.CreateMeterProviderBuilder().AddLwXDataInstrumentation().Build();
        Assert.NotNull(tracer);
        Assert.NotNull(meter);
    }

    [Fact]
    public void DoctorReport_UsesStableHeadingAndSafeConnectionText()
    {
        var report = new DoctorReport
        {
            Provider = "sqlite",
            SafeConnectionString = "Data Source=test.db"
        };
        report.Lines.Add("Connectivity: OK");

        var text = report.ToText();
        Assert.Contains("LwXLab.Data Doctor", text);
        Assert.Contains("Connectivity: OK", text);
    }

    [Fact]
    public async Task ProductionDeleteGuard_BlocksBeforeDatabaseMutation()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<ModelContext>("Data Source=:memory:");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => context.EnsureDeletedSafelyAsync(DatabaseEnvironment.Production));
    }

    public sealed class PostgreSqlContext : LwXDbContext<PostgreSqlContext>
    {
        public PostgreSqlContext(DbContextOptions<PostgreSqlContext> options, LwXDbContextDescriptor<PostgreSqlContext> descriptor)
            : base(options, descriptor) { }
    }

    public sealed class SqlServerContext : LwXDbContext<SqlServerContext>
    {
        public SqlServerContext(DbContextOptions<SqlServerContext> options, LwXDbContextDescriptor<SqlServerContext> descriptor)
            : base(options, descriptor) { }
    }

    public sealed class OracleContext : LwXDbContext<OracleContext>
    {
        public OracleContext(DbContextOptions<OracleContext> options, LwXDbContextDescriptor<OracleContext> descriptor)
            : base(options, descriptor) { }
    }

    public sealed class ModelContext : LwXDbContext<ModelContext>
    {
        public ModelContext(DbContextOptions<ModelContext> options, LwXDbContextDescriptor<ModelContext> descriptor)
            : base(options, descriptor) { }

        public DbSet<TestEntity> Entities => Set<TestEntity>();
    }

    public sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
