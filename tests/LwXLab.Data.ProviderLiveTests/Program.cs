using System.Data.Common;
using LwXLab.Data;
using LwXLab.Data.AI;
using LwXLab.Data.Diagnostics;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.MariaDb;
using LwXLab.Data.MySql;
using LwXLab.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

// Run only with the disposable database/file created by Test-LocalProviders.ps1.
var connectionString = Environment.GetEnvironmentVariable("LWX_PROVIDER_CONNECTION")
    ?? throw new InvalidOperationException("Set LWX_PROVIDER_CONNECTION.");
await using (DbConnection connection = ProbeModel.Provider switch
{
    "Sqlite" => await new SqliteConnectionFactory(new(connectionString)).OpenConnectionAsync(),
    "MySql" => await new MySqlConnectionFactory(new(connectionString)).OpenConnectionAsync(),
    "MariaDb" => await new MariaDbConnectionFactory(new(connectionString) { RequireSsl = false }).OpenConnectionAsync(),
    _ => throw new InvalidOperationException("Unknown provider.")
})
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1";
    Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "ADO.NET factory connectivity");
    Console.WriteLine("Server version: " + connection.ServerVersion);
#if NET10_0
    if (ProbeModel.Provider == "MariaDb")
    {
        command.CommandText = "CREATE TABLE Probe (Id int PRIMARY KEY, Name varchar(128) NOT NULL)";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "INSERT INTO Probe VALUES (1, @name)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = "private-row";
        command.Parameters.Add(parameter);
        Check(await command.ExecuteNonQueryAsync() == 1, "ADO.NET insert");
        command.Parameters.Clear();
        command.CommandText = "SELECT Name FROM Probe WHERE Id=1";
        Check((string?)await command.ExecuteScalarAsync() == "private-row", "ADO.NET read");
        command.CommandText = "UPDATE Probe SET Name='updated' WHERE Id=1";
        Check(await command.ExecuteNonQueryAsync() == 1, "ADO.NET update");
        command.CommandText = "SELECT Name FROM Probe WHERE Id=1";
        Check((string?)await command.ExecuteScalarAsync() == "updated", "ADO.NET updated read");
        command.CommandText = "DELETE FROM Probe WHERE Id=1";
        Check(await command.ExecuteNonQueryAsync() == 1, "ADO.NET delete");
        command.CommandText = "DROP TABLE Probe";
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("LIVE MariaDb PASS: ADO.NET only on .NET 10; EF Core registration is not available.");
        return;
    }
#endif
}
var services = new ServiceCollection();
services.AddLwXDbContext<ProbeContext>(b =>
{
    b.UseName("DisposableProviderTest");
    switch (ProbeModel.Provider)
    {
        case "Sqlite": b.UseSqlite(connectionString); break;
        case "MySql": b.UseMySql(connectionString); break;
#if !NET10_0
        case "MariaDb": b.UseMariaDb(connectionString, o => o.RequireSsl = false); break;
#endif
        default: throw new InvalidOperationException("EF provider unavailable.");
    }
});
services.AddLwXDataDiagnostics(o => { o.CheckDrift = true; o.EnableQueryBudget = true; });
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var context = scope.ServiceProvider.GetRequiredService<ProbeContext>();
Check(await context.Database.CanConnectAsync(), "EF connectivity");
var safety = await MigrationSafetyAnalyzer.AnalyzeAsync(context);
Check(safety.PendingMigrations.Count == 1 && safety.HighestRisk == MigrationRisk.Low, "migration safety");
await context.MigrateSafelyAsync(DatabaseEnvironment.Development);
Check((await context.Database.GetAppliedMigrationsAsync()).Count() == 1, "migration history");
context.Probes.Add(new Probe { Id = 1, Name = "private-row" });
await context.SaveChangesAsync();
Check((await context.Probes.SingleAsync()).Name == "private-row", "insert/read");
var row = await context.Probes.SingleAsync();
row.Name = "updated";
await context.SaveChangesAsync();
Check((await context.Probes.AsNoTracking().SingleAsync()).Name == "updated", "update");
using (provider.GetRequiredService<IQueryBudgetAccessor>().BeginScope("provider-live"))
{
    await context.Probes.CountAsync();
    await context.Probes.CountAsync();
    Check(provider.GetRequiredService<IQueryBudgetAccessor>().Current!.QueryCount == 2, "query budget");
}
Check((await DatabaseContractValidator.ValidateAsync(context, new DatabaseContract().RequireTable("Probe").RequireColumn("Probe", "Name"))).IsSatisfied, "contract");
Check(!AiSchemaContextFactory.Create(context, ProbeModel.Provider).ToJson().Contains("private-row"), "metadata-only AI");
var report = (await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync()).Single();
foreach (var check in report.Checks) Console.WriteLine($"{check.Check}: {check.Status} {check.Message}");
Check(!report.HasFailures, "diagnostics");
Check(report.Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Passed, "baseline drift");
await context.Database.ExecuteSqlRawAsync("ALTER TABLE Probe ADD COLUMN Extra int NULL");
report = (await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync()).Single();
Check(report.Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Warning, "intentional drift");
await context.Database.ExecuteSqlRawAsync("ALTER TABLE Probe DROP COLUMN Extra");
Check(await context.Probes.ExecuteDeleteAsync() == 1, "delete");
await context.GetService<IMigrator>().MigrateAsync("0");
Check(!(await context.Database.GetAppliedMigrationsAsync()).Any(), "migration rollback");
Console.WriteLine("LIVE " + ProbeModel.Provider + " PASS: factories, migrations, CRUD, diagnostics, drift, contracts, budgets and AI.");

static void Check(bool success, string feature)
{
    if (!success) throw new InvalidOperationException("Live verification failed: " + feature);
    Console.WriteLine("PASS " + feature);
}
public sealed class Probe { public int Id { get; set; } public string Name { get; set; } = ""; }
public static class ProbeModel
{
    public static string Provider => Environment.GetEnvironmentVariable("LWX_PROVIDER") ?? "Sqlite";
    public static string NameType => Provider == "Sqlite" ? "TEXT" : "varchar(128)";
    public static string IdType => Provider == "Sqlite" ? "INTEGER" : "int";
    public static void Configure(ModelBuilder model) => model.Entity<Probe>(e =>
    {
        e.ToTable("Probe");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasColumnType(IdType).ValueGeneratedNever();
        e.Property(p => p.Name).HasColumnType(NameType).HasMaxLength(128).IsRequired();
    });
}
public sealed class ProbeContext(DbContextOptions<ProbeContext> options, LwXDbContextDescriptor<ProbeContext> descriptor) : LwXDbContext<ProbeContext>(options, descriptor)
{
    public DbSet<Probe> Probes => Set<Probe>();
    protected override void ConfigureModel(ModelBuilder model) => ProbeModel.Configure(model);
}
[DbContext(typeof(ProbeContext))]
[Migration("20260927000200_ProviderProbe")]
public sealed class ProbeMigration : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.CreateTable("Probe",
        columns: table => new { Id = table.Column<int>(type: ProbeModel.IdType, nullable: false), Name = table.Column<string>(type: ProbeModel.NameType, maxLength: 128, nullable: false) },
        constraints: table => table.PrimaryKey("PK_Probe", x => x.Id));
    protected override void Down(MigrationBuilder migration) => migration.DropTable("Probe");
    protected override void BuildTargetModel(ModelBuilder model) => ProbeModel.Configure(model);
}
[DbContext(typeof(ProbeContext))]
public sealed class ProbeSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => ProbeModel.Configure(model);
}
