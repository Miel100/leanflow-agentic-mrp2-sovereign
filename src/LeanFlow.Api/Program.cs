using LeanFlow.Application.Agents;
using LeanFlow.Application.Services;
using LeanFlow.Api.Scheduling;
using LeanFlow.Infrastructure.Data;
using LeanFlow.Domain.Entities;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Build connection string from Railway PG variables
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE");
var pgUser = Environment.GetEnvironmentVariable("PGUSER");
var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD");
var localDb = Environment.GetEnvironmentVariable("DB_CONNECTION");

bool useDatabase = !string.IsNullOrEmpty(pgHost) || !string.IsNullOrEmpty(localDb);
Console.WriteLine($"DEBUG: pgHost={pgHost}, useDatabase={useDatabase}");

if (useDatabase)
{
    string connectionString;
    if (!string.IsNullOrEmpty(pgHost))
        connectionString = $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword};SSL Mode=Require;Trust Server Certificate=true";
    else
        connectionString = localDb!;
    builder.Services.AddDbContext<LeanMRP2DbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<LeanMRP2Service>();
builder.Services.AddScoped<DemandAgent>();
builder.Services.AddScoped<RCCPAgent>();
builder.Services.AddScoped<CRPAgent>();
builder.Services.AddScoped<SFCAgent>();
builder.Services.AddScoped<SupervisorAgent>();
builder.Services.AddScoped<MRPScheduler>();
builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

// Seed database only if available
if (useDatabase)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeanMRP2DbContext>();
        db.Database.EnsureCreated();
        if (!db.RatingFiles.Any())
        {
            db.RatingFiles.AddRange(
                new RatingFile { ItemCode = "ITEM-001", Description = "Steel Frame Assembly", ProcessingTime = 2.5m, SetupTime = 0.5m, BatchQuantity = 100, UtilizationTarget = 0.85m, MachineGroup = "PRESS-01", ShiftPerDay = 3, CostPerUnit = 45.50m },
                new RatingFile { ItemCode = "ITEM-002", Description = "Hydraulic Pump Unit", ProcessingTime = 4.0m, SetupTime = 1.0m, BatchQuantity = 50, UtilizationTarget = 0.80m, MachineGroup = "LATHE-02", ShiftPerDay = 2, CostPerUnit = 120.00m },
                new RatingFile { ItemCode = "ITEM-003", Description = "Electronic Control Board", ProcessingTime = 1.5m, SetupTime = 0.25m, BatchQuantity = 200, UtilizationTarget = 0.90m, MachineGroup = "SMT-01", ShiftPerDay = 3, CostPerUnit = 85.00m },
                new RatingFile { ItemCode = "ITEM-004", Description = "Aluminium Housing", ProcessingTime = 3.0m, SetupTime = 0.75m, BatchQuantity = 75, UtilizationTarget = 0.82m, MachineGroup = "CNC-03", ShiftPerDay = 2, CostPerUnit = 67.25m },
                new RatingFile { ItemCode = "ITEM-005", Description = "Conveyor Belt Module", ProcessingTime = 5.5m, SetupTime = 1.5m, BatchQuantity = 30, UtilizationTarget = 0.78m, MachineGroup = "WELD-01", ShiftPerDay = 1, CostPerUnit = 210.00m }
            );
            db.InventoryRecords.AddRange(
                new InventoryRecord { ItemCode = "ITEM-001", Description = "Steel Frame Assembly", CurrentStock = 450, SafetyStock = 100, ReorderPoint = 150 },
                new InventoryRecord { ItemCode = "ITEM-002", Description = "Hydraulic Pump Unit", CurrentStock = 80, SafetyStock = 20, ReorderPoint = 40 },
                new InventoryRecord { ItemCode = "ITEM-003", Description = "Electronic Control Board", CurrentStock = 320, SafetyStock = 50, ReorderPoint = 80 },
                new InventoryRecord { ItemCode = "ITEM-004", Description = "Aluminium Housing", CurrentStock = 120, SafetyStock = 30, ReorderPoint = 60 },
                new InventoryRecord { ItemCode = "ITEM-005", Description = "Conveyor Belt Module", CurrentStock = 15, SafetyStock = 5, ReorderPoint = 10 }
            );
            db.SaveChanges();
            Console.WriteLine("Database seeded successfully");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database connection failed: {ex.Message} - running without persistence");
    }
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHangfireDashboard("/hangfire");
app.UseCors();
RecurringJob.AddOrUpdate<MRPScheduler>("daily-mrp-cycle", x => x.RunDailyMRPCycleAsync(), Cron.Daily);
app.UseAuthorization();
app.MapControllers();
app.Run();

