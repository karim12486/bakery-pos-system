using Nizam.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog (console + rolling daily file under logs/, kept 30 days).
builder.Host.AddNizamSerilog();

// DI registrations — one extension method per concern. Each method is independently
// testable and trivial to add tenancy / observability hooks to later.
builder.Services
    .AddNizamCors(builder.Configuration)
    .AddNizamPersistence(builder.Configuration)
    .AddNizamApplicationServices()
    .AddNizamAuthentication(builder.Configuration)
    .AddNizamRealtime()
    .AddNizamRateLimiting(builder.Configuration)
    .AddNizamApi()
    .AddNizamHealthChecks()
    .AddNizamHangfire(builder.Configuration);

var app = builder.Build();

// Apply migrations + seed admin (idempotent).
await app.SeedDatabaseAsync();

// Hangfire schedule — upsert by job id, runs on every startup so missing schedules are
// recovered (e.g. after a fresh deploy or a Hangfire schema reset).
app.ScheduleNizamRecurringJobs();

// Request pipeline + endpoint mapping.
app.UseNizamPipeline();
app.UseNizamHangfireDashboard();
app.MapNizamEndpoints();
app.UseNizamLocalization();

app.Run();

public partial class Program { }
