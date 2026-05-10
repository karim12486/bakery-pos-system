using BakeryPOS.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog (console + rolling daily file under logs/, kept 30 days).
builder.Host.AddBakeryPosSerilog();

// DI registrations — one extension method per concern. Each method is independently
// testable and trivial to add tenancy / observability hooks to later.
builder.Services
    .AddBakeryPosCors(builder.Configuration)
    .AddBakeryPosPersistence(builder.Configuration)
    .AddBakeryPosApplicationServices()
    .AddBakeryPosAuthentication(builder.Configuration)
    .AddBakeryPosRealtime()
    .AddBakeryPosRateLimiting(builder.Configuration)
    .AddBakeryPosApi()
    .AddBakeryPosHealthChecks();

var app = builder.Build();

// Apply migrations + seed admin (idempotent).
await app.SeedDatabaseAsync();

// Request pipeline + endpoint mapping.
app.UseBakeryPosPipeline();
app.MapBakeryPosEndpoints();
app.UseBakeryPosLocalization();

app.Run();

public partial class Program { }
