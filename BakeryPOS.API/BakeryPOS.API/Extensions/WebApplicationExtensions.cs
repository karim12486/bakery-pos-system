using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BakeryPOS.API.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Wires the request pipeline in the canonical order: Swagger UI → CORS → RateLimiter →
    /// AuthN → AuthZ → static files. HTTPS redirect is intentionally OFF for local LAN HTTP;
    /// see README.
    /// </summary>
    public static WebApplication UseBakeryPosPipeline(this WebApplication app)
    {
        // Swagger is enabled in production too, by design — the operator uses it to seed data
        // on the client's PC. Re-evaluate when self-serve onboarding lands.
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BakeryPOS API V1");
            c.RoutePrefix = string.Empty;
        });

        // app.UseHttpsRedirection(); // Intentionally off for local LAN HTTP. See README.
        app.UseCors(ApiExtensions.CorsPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseBakeryPosStaticFiles();

        return app;
    }

    /// <summary>
    /// Maps API controllers, SignalR hubs, and the health-check endpoints
    /// (<c>/health/live</c>, <c>/health/ready</c>).
    /// </summary>
    public static WebApplication MapBakeryPosEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapBakeryPosHubs();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Liveness intentionally ignores tagged checks — a DB hiccup shouldn't restart the process.
            Predicate = _ => false
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }

    /// <summary>
    /// Sets the default thread culture. Today: French (Morocco) — left over from the freelance
    /// bakery customer. The SaaS migration will replace this with per-tenant culture middleware
    /// driven by <c>Tenant.Locale</c>.
    /// </summary>
    public static WebApplication UseBakeryPosLocalization(this WebApplication app)
    {
        var defaultCulture = new CultureInfo("fr-MA");
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
        return app;
    }
}
