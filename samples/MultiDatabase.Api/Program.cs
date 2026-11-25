using LwXLab.Data;
using LwXLab.Data.AspNetCore;
using LwXLab.Data.Diagnostics;
using MultiDatabase.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var supabase = builder.Configuration.GetConnectionString("SharedData")
    ?? throw new InvalidOperationException("ConnectionStrings:SharedData is required.");
var internalSqlServer = builder.Configuration.GetConnectionString("InternalSqlServer")
    ?? throw new InvalidOperationException("ConnectionStrings:InternalSqlServer is required.");

builder.Services.AddSupabaseDbContext<CatalogDbContext>(supabase, "catalog", o => { o.ApplicationName = "catalog-api"; o.MaximumPoolSize = 5; });
builder.Services.AddSqlServerDbContext<InternalDbContext>(internalSqlServer, "internal", o => { o.ApplicationName = "sample-internal-api"; o.MaximumPoolSize = 10; });

// Entire diagnostics stack is optional. These beginner-safe defaults only observe/report.
builder.Services.AddLwXDataDiagnostics();
builder.Services.AddLwXDataHealthCheck();

var app = builder.Build();
app.UseLwXDataQueryBudget(); // No effect unless EnableQueryBudget=true.
app.MapLwXDataHealth();
app.MapGet("/", () => Results.Ok(new { message = "LwXLab.Data multi-database sample" }));
app.Run();
