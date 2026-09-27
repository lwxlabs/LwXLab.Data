using System.Data;
using LwXLab.Data.AI;
using LwXLab.Data.Diagnostics;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using LwXLab.Data.AspNetCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class FeatureBehaviorTests
{
    [Fact]
    public async Task DriftDiagnostics_IgnoreConfiguredMigrationHistoryTable()
    {
        var services = new ServiceCollection();
        services.AddLwXDbContext<IntegrationSurfaceTests.ModelContext>(b => b.UseSqlite("Data Source=drift-history;Mode=Memory;Cache=Shared").UseMigrationsHistoryTable("AppHistory"));
        services.AddLwXDataDiagnostics(o => { o.CheckMigrations = false; o.CheckDrift = true; });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationSurfaceTests.ModelContext>();
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE AppHistory (MigrationId TEXT, ProductVersion TEXT)");
        var report = Assert.Single(await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync());
        Assert.Equal(DatabaseCheckStatus.Passed, report.Checks.Single(c => c.Check == "drift").Status);
    }
    [Fact]
    public async Task PendingDestructiveMigration_IsClassifiedAndBlockedInProduction()
    {
        await using var context = new MigrationContext(new DbContextOptionsBuilder<MigrationContext>().UseSqlite("Data Source=:memory:").Options);
        await context.Database.OpenConnectionAsync();
        var report = await MigrationSafetyAnalyzer.AnalyzeAsync(context);
        Assert.Single(report.PendingMigrations);
        Assert.Equal(MigrationRisk.High, report.HighestRisk);
        Assert.Contains(report.Findings, x => x.Code == "migration.drop_table");
        Assert.Contains("High", AiMigrationReview.From(report).ToJson());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.MigrateSafelyAsync(DatabaseEnvironment.Production));
    }
    [Theory]
    [InlineData(DatabaseCheckStatus.Passed, HealthStatus.Healthy)]
    [InlineData(DatabaseCheckStatus.Warning, HealthStatus.Degraded)]
    [InlineData(DatabaseCheckStatus.Failed, HealthStatus.Unhealthy)]
    public async Task HealthCheck_MapsDiagnosticStatus(DatabaseCheckStatus check, HealthStatus health)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILwXDataDiagnostics>(new FailedDiagnostics(check));
        services.AddLwXDataHealthCheck();
        using var provider = services.BuildServiceProvider();
        Assert.Equal(health, (await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Status);
    }

    [Fact]
    public async Task SqliteFactory_CreatesUsableContext()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContextFactory<IntegrationSurfaceTests.ModelContext>("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        await using var context = await provider.GetRequiredService<IDbContextFactory<IntegrationSurfaceTests.ModelContext>>().CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        Assert.Empty(await context.Entities.ToListAsync());
    }
    [Theory]
    [InlineData("Password=\"secret;still-secret")]
    [InlineData("Pwd=secret;invalid")]
    [InlineData("Token='secret;more-secret")]
    [InlineData("Host=localhost;API Key=secret")]
    public void MalformedAndAlternateSecrets_AreNeverEchoed(string connection)
        => Assert.DoesNotContain("secret", ConnectionStringRedactor.Redact(connection));

    [Fact]
    public async Task SqliteCrud_Drift_Contract_AndMetadata_WorkTogether()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<IntegrationSurfaceTests.ModelContext>("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationSurfaceTests.ModelContext>();
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        context.Entities.Add(new IntegrationSurfaceTests.TestEntity { Name = "private-row" });
        await context.SaveChangesAsync();
        Assert.Equal("private-row", (await context.Entities.SingleAsync()).Name);
        var model = DatabaseDriftDetector.FromEfModel(context, null);
        var live = await DatabaseDriftDetector.FromDatabaseAsync(context.Database.GetDbConnection(), "sqlite", null);
        Assert.Empty(DatabaseDriftDetector.Diff(model, live));
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE Entities ADD COLUMN Extra TEXT");
        live = await DatabaseDriftDetector.FromDatabaseAsync(context.Database.GetDbConnection(), "sqlite", null);
        Assert.Contains(DatabaseDriftDetector.Diff(model, live), x => x.Contains("Extra"));
        var contract = new DatabaseContract().RequireTable("Entities").RequireColumn("Entities", "Name")
            .RequireMinimumServerVersion(new Version(3, 0));
        Assert.True((await DatabaseContractValidator.ValidateAsync(context, contract)).IsSatisfied);
        var missing = await DatabaseContractValidator.ValidateAsync(context, new DatabaseContract().RequireColumn("Entities", "Missing"));
        Assert.Contains("column Entities.Missing", missing.Missing);
        Assert.Equal(ConnectionState.Open, context.Database.GetDbConnection().State);
        Assert.DoesNotContain("private-row", AiSchemaContextFactory.Create(context, "sqlite").ToJson());
        Assert.Single(AiDevelopmentSeedBlueprint.Create(context));
        Assert.Contains("Sqlite", LwXDataAgentManifest.FromDescriptors(provider.GetServices<ILwXDbContextDescriptor>()).ToJson());
        Assert.Empty((await MigrationSafetyAnalyzer.AnalyzeAsync(context)).PendingMigrations);
        await context.Entities.ExecuteDeleteAsync();
        Assert.Empty(await context.Entities.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryBudget_ObservesOrThrowsOnlyWhenConfigured(bool shouldThrow)
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<IntegrationSurfaceTests.ModelContext>("Data Source=:memory:");
        services.AddLwXDataDiagnostics(o =>
        {
            o.EnableQueryBudget = true;
            o.QueryBudgetWarningCount = 1;
            o.QueryBudgetMaximumCount = 1;
            o.ThrowWhenQueryBudgetExceeded = shouldThrow;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationSurfaceTests.ModelContext>();
        var budget = provider.GetRequiredService<IQueryBudgetAccessor>();
        using (budget.BeginScope("outer"))
        {
            var outer = budget.Current;
            await context.Database.ExecuteSqlRawAsync("SELECT 1");
            if (shouldThrow)
                await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.ExecuteSqlRawAsync("SELECT 1"));
            else
                await context.Database.ExecuteSqlRawAsync("SELECT 1");
            Assert.Equal(2, outer!.QueryCount);
            using (budget.BeginScope("inner")) Assert.Equal(0, budget.Current!.QueryCount);
            Assert.Same(outer, budget.Current);
        }
        Assert.Null(budget.Current);
    }

    [Fact]
    public async Task ExplicitProductionGuard_BlocksDestructiveSql()
    {
        var services = new ServiceCollection();
        services.AddSqliteDbContext<IntegrationSurfaceTests.ModelContext>("Data Source=:memory:");
        services.AddLwXDataDiagnostics(o =>
        {
            o.EnableEnvironmentProtection = true;
            o.BlockDestructiveCommandsInProduction = true;
            o.Environment = DatabaseEnvironment.Production;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationSurfaceTests.ModelContext>();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE KeepMe (Id INTEGER)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.ExecuteSqlRawAsync("DROP TABLE KeepMe"));
        await context.Database.ExecuteSqlRawAsync("INSERT INTO KeepMe VALUES (1)");
    }

    [Theory]
    [InlineData(DatabaseStartupMode.None, false, 0)]
    [InlineData(DatabaseStartupMode.Validate, false, 1)]
    [InlineData(DatabaseStartupMode.Strict, true, 1)]
    public async Task StartupModes_WorkAsDocumented(DatabaseStartupMode mode, bool throws, int calls)
    {
        var diagnostics = new FailedDiagnostics();
        var services = new ServiceCollection();
        services.AddSingleton<ILwXDataDiagnostics>(diagnostics);
        services.AddLwXDataDiagnostics();
        services.AddLwXDataStartupChecks(o => o.Mode = mode);
        using var provider = services.BuildServiceProvider();
        var startup = Assert.Single(provider.GetServices<IHostedService>());
        if (throws) await Assert.ThrowsAsync<InvalidOperationException>(() => startup.StartAsync(default));
        else await startup.StartAsync(default);
        Assert.Equal(calls, diagnostics.Calls);
    }

    private sealed class FailedDiagnostics : ILwXDataDiagnostics
    {
        private readonly DatabaseCheckStatus _status;
        public FailedDiagnostics(DatabaseCheckStatus status = DatabaseCheckStatus.Failed) { _status = status; }
        public int Calls { get; private set; }
        public Task<IReadOnlyList<DatabaseDiagnosticReport>> RunAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            var report = new DatabaseDiagnosticReport("test", "sqlite", null);
            report.Checks.Add(new DatabaseCheckResult("connectivity", _status, "test result"));
            return Task.FromResult<IReadOnlyList<DatabaseDiagnosticReport>>(new[] { report });
        }
    }
}

public sealed class MigrationContext(DbContextOptions<MigrationContext> options) : DbContext(options);

[DbContext(typeof(MigrationContext))]
[Migration("20260927000000_Destructive")]
public sealed class DestructiveTestMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ExistingTable");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable("ExistingTable", columns: table => new { Id = table.Column<int>(nullable: false) });
}
