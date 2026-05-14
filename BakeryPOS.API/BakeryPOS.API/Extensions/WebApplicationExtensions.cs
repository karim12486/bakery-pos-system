using System.Globalization;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Common.Localization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BakeryPOS.API.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Wires the request pipeline in the canonical order: ProblemDetails → Swagger UI → CORS →
    /// RateLimiter → AuthN → AuthZ → TenantCulture → static files. HTTPS redirect is intentionally
    /// OFF for local LAN HTTP; see README.
    /// </summary>
    public static WebApplication UseBakeryPosPipeline(this WebApplication app)
    {
        // Exception → ProblemDetails (RFC 7807) translation must run BEFORE anything that can throw
        // downstream — including auth, controllers, and FluentValidation in services.
        app.UseMiddleware<ProblemDetailsMiddleware>();

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

        // Per-tenant culture — runs AFTER auth so tenant_id claim is available.
        // Reads Tenant.Locale (or Accept-Language override) and sets CurrentCulture for the
        // request scope. Replaces the legacy hard-coded fr-MA default.
        app.UseMiddleware<TenantCultureMiddleware>();

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
    /// Sets the PROCESS-WIDE default culture — used by background services (hosted services,
    /// the seeder) that run outside any HTTP request and therefore don't go through the
    /// per-request <see cref="TenantCultureMiddleware"/>.
    ///
    /// Defaults to Egyptian Arabic to match the SaaS launch market. Per-request culture for
    /// authenticated calls is set by <see cref="TenantCultureMiddleware"/> from
    /// <c>Tenant.Locale</c>.
    /// </summary>
    public static WebApplication UseBakeryPosLocalization(this WebApplication app)
    {
        var defaultCulture = new CultureInfo("ar-EG");
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
        return app;
    }
}
