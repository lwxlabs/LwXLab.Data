using LwXLab.Data;
using LwXLab.Data.AI;
using LwXLab.Data.Diagnostics;
using LwXLab.Data.EntityFrameworkCore;
using LwXLab.Data.Supabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

// SQL identifiers below pass DatabaseIdentifier.ValidatePostgreSql; row values use parameters.
#pragma warning disable EF1002

// Requires a disposable private schema and a login restricted to that schema.
// Never run this against an application's schema. Credentials are environment-only.
var connectionString = Environment.GetEnvironmentVariable("LWX_DATA_LIVE_CONNECTION")
    ?? throw new InvalidOperationException("Set LWX_DATA_LIVE_CONNECTION.");
var schema = LiveModel.Schema;
var otherSchema = DatabaseIdentifier.ValidatePostgreSql(schema + "_b");
var services = new ServiceCollection();
services.AddLwXDbContext<LiveContext>(b =>
{
    b.UseName("SharedData").UseSchema(schema);
    if (LiveModel.IsSqlServer)
        b.UseSqlServer(connectionString, o => o.TrustServerCertificate = Environment.GetEnvironmentVariable("LWX_DATA_LIVE_TRUST_LOCAL_CERTIFICATE") == "1");
    else b.UseSupabase(connectionString);
});
services.AddLwXDataDiagnostics(o => { o.CheckPermissions = true; o.CheckDrift = true; o.EnableQueryBudget = true; });
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var context = scope.ServiceProvider.GetRequiredService<LiveContext>();
Check(await context.Database.CanConnectAsync(), "connectivity");
var connection = context.Database.GetDbConnection();
var permissions = await SchemaIsolationChecker.CheckAsync(connection, LiveModel.Provider, schema);
Check(permissions.Supported && permissions.CanUseSchema && permissions.CanCreateInSchema, "schema permissions");
var safety = await MigrationSafetyAnalyzer.AnalyzeAsync(context);
Check(safety.PendingMigrations.Count == 1 && safety.HighestRisk == MigrationRisk.Low, "pending migration analysis");
await context.MigrateSafelyAsync(DatabaseEnvironment.Development);
Check(!(await context.Database.GetPendingMigrationsAsync()).Any(), "migration applied");
Check((await context.Database.GetAppliedMigrationsAsync()).Count() == 1, "schema-local migration history");
var id = Guid.NewGuid();
context.Probes.Add(new Probe { Id = id, Name = "live-private-row" });
await context.SaveChangesAsync();
Check((await context.Probes.SingleAsync()).Name == "live-private-row", "insert/read");
var item = await context.Probes.SingleAsync();
item.Name = "updated";
await context.SaveChangesAsync();
Check((await context.Probes.AsNoTracking().SingleAsync()).Name == "updated", "update");
var budget = provider.GetRequiredService<IQueryBudgetAccessor>();
using (budget.BeginScope("live"))
{
    await context.Probes.CountAsync();
    await context.Probes.CountAsync();
    Check(budget.Current!.QueryCount == 2, "live query budget");
}
var contract = new DatabaseContract().RequireTable("Probe", schema).RequireColumn("Probe", "Name", schema).RequireMinimumServerVersion(new Version(LiveModel.IsSqlServer ? 16 : 17, 0));
Check((await DatabaseContractValidator.ValidateAsync(context, contract, schema)).IsSatisfied, "database contract");
var payload = AiSchemaContextFactory.Create(context, LiveModel.Provider, schema).ToJson();
Check(!payload.Contains("live-private-row") && !payload.Contains(connectionString), "metadata-only AI payload");
await context.Database.ExecuteSqlRawAsync($"CREATE TABLE \"{otherSchema}\".\"Probe\" (\"Id\" {LiveModel.IdType} PRIMARY KEY, \"Name\" {LiveModel.NameType} NOT NULL)");
await context.Database.ExecuteSqlRawAsync($"INSERT INTO \"{otherSchema}\".\"Probe\" VALUES ({{0}}, 'other-app')", Guid.NewGuid());
Check(await context.Probes.CountAsync() == 1, "other-app schema isolation");
var reports = await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync();
Check(reports.Count == 1 && !reports[0].HasFailures, "live diagnostics");
foreach (var check in reports[0].Checks) Console.WriteLine($"{check.Check}: {check.Status} {check.Message}");
Check(reports[0].Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Passed, "no false-positive schema drift");
if (LiveModel.IsSqlServer)
{
    await context.Database.ExecuteSqlRawAsync($"ALTER TABLE [{schema}].[Probe] ALTER COLUMN [Name] nvarchar(32) NOT NULL");
    reports = await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync();
    Check(reports[0].Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Warning, "SQL Server length drift detected");
    await context.Database.ExecuteSqlRawAsync($"ALTER TABLE [{schema}].[Probe] ALTER COLUMN [Name] nvarchar(max) NOT NULL");
    await context.Database.ExecuteSqlRawAsync($"ALTER TABLE [{schema}].[Probe] ALTER COLUMN [Amount] decimal(12,2) NOT NULL");
    reports = await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync();
    Check(reports[0].Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Warning, "SQL Server decimal precision drift detected");
    await context.Database.ExecuteSqlRawAsync($"ALTER TABLE [{schema}].[Probe] ALTER COLUMN [Amount] decimal(18,2) NOT NULL");
}
await context.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{schema}\".\"Probe\" ADD { (LiveModel.IsSqlServer ? "" : "COLUMN ") }\"Extra\" {LiveModel.NameType}");
reports = await provider.GetRequiredService<ILwXDataDiagnostics>().RunAsync();
Check(reports[0].Checks.Single(c => c.Check == "drift").Status == DatabaseCheckStatus.Warning, "intentional drift detected");
await context.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{schema}\".\"Probe\" DROP COLUMN \"Extra\"");
Check(await context.Probes.ExecuteDeleteAsync() == 1, "delete");
if (!LiveModel.IsSqlServer)
{
var transactional = new NpgsqlConnectionStringBuilder(connectionString) { Port = 6543, MaxAutoPrepare = 10 };
var normalized = SupabaseConnectionString.Create(new SupabaseDatabaseOptions(transactional.ConnectionString));
Check(new NpgsqlConnectionStringBuilder(normalized).MaxAutoPrepare == 0, "transaction-pooler preparation disabled");
await using (var pooled = new SupabaseConnectionFactory(new SupabaseDatabaseOptions(normalized)).CreateConnection())
{
    await pooled.OpenAsync();
    await using var command = pooled.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM \"{schema}\".\"Probe\"";
    Check(Convert.ToInt64(await command.ExecuteScalarAsync()) == 0, "transaction-pooler query");
}
}
await context.GetService<IMigrator>().MigrateAsync("0");
await context.Database.ExecuteSqlRawAsync($"DROP TABLE \"{otherSchema}\".\"Probe\"");
await context.Database.ExecuteSqlRawAsync($"DROP TABLE \"{schema}\".\"__EFMigrationsHistory\"");
Console.WriteLine("LIVE " + LiveModel.Provider + " PASS: migrations, CRUD, schema isolation, diagnostics, drift, contracts, budgets and AI.");

static void Check(bool condition, string feature)
{
    if (!condition) throw new InvalidOperationException("Live verification failed: " + feature);
    Console.WriteLine("PASS " + feature);
}

public sealed class Probe { public Guid Id { get; set; } public string Name { get; set; } = ""; public decimal Amount { get; set; } }
public static class LiveModel
{
    public static bool IsSqlServer => Environment.GetEnvironmentVariable("LWX_DATA_LIVE_PROVIDER") == "SqlServer";
    public static string Provider => IsSqlServer ? "SqlServer" : "Supabase";
    public static string IdType => IsSqlServer ? "uniqueidentifier" : "uuid";
    public static string NameType => IsSqlServer ? "nvarchar(max)" : "text";
    public static string Schema => DatabaseIdentifier.ValidatePostgreSql(Environment.GetEnvironmentVariable("LWX_DATA_LIVE_SCHEMA") ?? throw new InvalidOperationException("Set LWX_DATA_LIVE_SCHEMA."));
    public static void Configure(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        if (IsSqlServer) model.UseIdentityColumns();
        else model.UseIdentityByDefaultColumns();
        model.Entity<Probe>(e =>
        {
            e.ToTable("Probe", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnType(IdType).ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnType(NameType).IsRequired();
            if (IsSqlServer) e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            else e.Ignore(x => x.Amount);
        });
    }
}
public sealed class LiveContext(DbContextOptions<LiveContext> options, LwXDbContextDescriptor<LiveContext> descriptor) : LwXDbContext<LiveContext>(options, descriptor)
{
    public DbSet<Probe> Probes => Set<Probe>();
    protected override void ConfigureModel(ModelBuilder model) => LiveModel.Configure(model);
}
[DbContext(typeof(LiveContext))]
[Migration("20260927000100_LiveProbe")]
public sealed class LiveProbeMigration : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        if (LiveModel.IsSqlServer)
            migration.CreateTable("Probe", schema: LiveModel.Schema,
                columns: table => new { Id = table.Column<Guid>(type: LiveModel.IdType, nullable: false), Name = table.Column<string>(type: LiveModel.NameType, nullable: false), Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false) },
                constraints: table => table.PrimaryKey("PK_Probe", x => x.Id));
        else
            migration.CreateTable("Probe", schema: LiveModel.Schema,
                columns: table => new { Id = table.Column<Guid>(type: LiveModel.IdType, nullable: false), Name = table.Column<string>(type: LiveModel.NameType, nullable: false) },
                constraints: table => table.PrimaryKey("PK_Probe", x => x.Id));
    }
    protected override void Down(MigrationBuilder migration) => migration.DropTable("Probe", LiveModel.Schema);
    protected override void BuildTargetModel(ModelBuilder model) => LiveModel.Configure(model);
}
[DbContext(typeof(LiveContext))]
public sealed class LiveSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model) => LiveModel.Configure(model);
}
