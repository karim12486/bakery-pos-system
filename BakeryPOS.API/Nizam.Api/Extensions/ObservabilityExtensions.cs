using Serilog;
using Serilog.Events;

namespace Nizam.Api.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Wires Serilog as the logging provider with sensible defaults:
    /// console sink always; rolling daily file sink under <c>logs/</c> kept 30 days;
    /// enriched with machine name and ASP.NET Core request id.
    /// Tenant/Branch/User enrichers will be added by middleware once tenancy lands.
    /// </summary>
    public static IHostBuilder AddNizamSerilog(this IHostBuilder host)
    {
        host.UseSerilog((ctx, services, lc) =>
        {
            lc
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .ReadFrom.Configuration(ctx.Configuration)   // appsettings can override anything
                .ReadFrom.Services(services)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(ctx.HostingEnvironment.ContentRootPath, "logs", "nizam-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
        });

        return host;
    }

    /// <summary>
    /// Adds liveness + readiness health checks. <c>/health/live</c> returns 200 if the process is up;
    /// <c>/health/ready</c> additionally checks the database connection.
    /// </summary>
    public static IServiceCollection AddNizamHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<Data.AppDbContext>("database", tags: new[] { "ready" });
        return services;
    }
}
