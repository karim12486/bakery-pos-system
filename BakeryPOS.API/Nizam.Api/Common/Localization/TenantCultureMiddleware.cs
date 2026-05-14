using System.Globalization;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Common.Localization;

/// <summary>
/// Resolves the request culture in this priority order:
/// <list type="number">
///   <item>The <c>Accept-Language</c> header, IF it matches one of our supported cultures —
///         lets the frontend explicitly request EN even though the tenant default is AR.</item>
///   <item>The current tenant's <c>Tenant.Locale</c> (e.g. <c>ar-EG</c>).</item>
///   <item>The hard-coded fallback <see cref="DefaultCulture"/>.</item>
/// </list>
/// Sets <see cref="CultureInfo.CurrentCulture"/> + <see cref="CultureInfo.CurrentUICulture"/>
/// for the duration of the request — formatting (currency, dates, numbers) and resource
/// strings then resolve in the right language without explicit threading.
///
/// Runs AFTER authentication so the <c>tenant_id</c> claim is available.
/// </summary>
public sealed class TenantCultureMiddleware
{
    private const string DefaultCulture = "ar-EG";

    /// <summary>Cultures the API is explicitly built to serve.</summary>
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "ar-EG", "en-US", "en-GB", "fr-FR", "fr-MA"
    };

    private readonly RequestDelegate _next;

    public TenantCultureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, ICurrentTenant currentTenant, AppDbContext db)
    {
        var culture = await ResolveCultureAsync(ctx, currentTenant, db);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        await _next(ctx);
    }

    private static async Task<CultureInfo> ResolveCultureAsync(HttpContext ctx, ICurrentTenant currentTenant, AppDbContext db)
    {
        // 1. Accept-Language explicit override — only honoured if it's a culture we support.
        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();
        if (!string.IsNullOrEmpty(acceptLang))
        {
            // Take the first quality-ranked language tag, strip the q-value if present.
            var primary = acceptLang.Split(',')[0].Split(';')[0].Trim();
            if (SupportedCultures.Contains(primary))
            {
                return TryGetCulture(primary) ?? new CultureInfo(DefaultCulture);
            }
        }

        // 2. Tenant default from Tenant.Locale.
        if (currentTenant.TenantId is int tid)
        {
            // IgnoreQueryFilters: Tenants table isn't filtered, but be explicit so refactors
            // don't break us.
            var locale = await db.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == tid)
                .Select(t => t.Locale)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(locale))
            {
                return TryGetCulture(locale) ?? new CultureInfo(DefaultCulture);
            }
        }

        // 3. Fallback.
        return new CultureInfo(DefaultCulture);
    }

    private static CultureInfo? TryGetCulture(string name)
    {
        try { return new CultureInfo(name); }
        catch (CultureNotFoundException) { return null; }
    }
}
