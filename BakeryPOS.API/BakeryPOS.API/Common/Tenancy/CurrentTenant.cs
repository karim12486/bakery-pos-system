using System.Security.Claims;

namespace BakeryPOS.API.Common.Tenancy;

/// <summary>
/// Reads <c>tenant_id</c> and <c>branch_id</c> claims from the current
/// <see cref="HttpContext.User"/>. Registered as scoped (one instance per request).
///
/// <para>Supports a per-scope override via <see cref="SetOverride"/> — used by background
/// services (e.g. <c>ScheduledReportService</c>) to temporarily impersonate a tenant while
/// iterating across all of them. The override takes precedence over the HttpContext claim.</para>
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    public const string TenantClaim = "tenant_id";
    public const string BranchClaim = "branch_id";

    private readonly IHttpContextAccessor _httpContext;
    private int? _tenantOverride;
    private int? _branchOverride;
    private bool _overrideSet;

    public CurrentTenant(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
    }

    public int? TenantId => _overrideSet
        ? _tenantOverride
        : ParseInt(_httpContext.HttpContext?.User?.FindFirst(TenantClaim)?.Value);

    public int? BranchId => _overrideSet
        ? _branchOverride
        : ParseInt(_httpContext.HttpContext?.User?.FindFirst(BranchClaim)?.Value);

    /// <summary>
    /// For background services and integration paths that need to set the tenant context
    /// programmatically within their own scope. NEVER call from HTTP request paths —
    /// the claim is the source of truth there.
    /// </summary>
    public void SetOverride(int? tenantId, int? branchId = null)
    {
        _tenantOverride = tenantId;
        _branchOverride = branchId;
        _overrideSet = true;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var id) ? id : null;
}

/// <summary>
/// Manual / out-of-band tenant context for tests and one-shot bootstrap paths.
/// Construct with <c>null</c> for genuinely anonymous operations — under the CLOSED
/// global query filter, this means queries return nothing, and any cross-tenant read
/// must use <c>IgnoreQueryFilters()</c> explicitly (auditable in code review).
/// </summary>
public sealed class AmbientTenant : ICurrentTenant
{
    public AmbientTenant(int? tenantId, int? branchId = null)
    {
        TenantId = tenantId;
        BranchId = branchId;
    }

    public int? TenantId { get; }
    public int? BranchId { get; }
}
