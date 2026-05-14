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
    .AddBakeryPosHealthChecks()
    .AddBakeryPosHangfire(builder.Configuration);

var app = builder.Build();

// Apply migrations + seed admin (idempotent).
await app.SeedDatabaseAsync();

// Hangfire schedule — upsert by job id, runs on every startup so missing schedules are
// recovered (e.g. after a fresh deploy or a Hangfire schema reset).
app.ScheduleBakeryPosRecurringJobs();

// Request pipeline + endpoint mapping.
app.UseBakeryPosPipeline();
app.UseBakeryPosHangfireDashboard();
app.MapBakeryPosEndpoints();
app.UseBakeryPosLocalization();

app.Run();

public partial class Program { }
