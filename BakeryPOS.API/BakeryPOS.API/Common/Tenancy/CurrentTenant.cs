using System.Security.Claims;

namespace BakeryPOS.API.Common.Tenancy;

/// <summary>
/// Reads <c>tenant_id</c> and <c>branch_id</c> claims from the current
/// <see cref="HttpContext.User"/>. Registered as scoped (one instance per request).
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    public const string TenantClaim = "tenant_id";
    public const string BranchClaim = "branch_id";

    private readonly IHttpContextAccessor _httpContext;

    public CurrentTenant(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
    }

    public int? TenantId => ParseInt(_httpContext.HttpContext?.User?.FindFirst(TenantClaim)?.Value);
    public int? BranchId => ParseInt(_httpContext.HttpContext?.User?.FindFirst(BranchClaim)?.Value);

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var id) ? id : null;
}

/// <summary>
/// Manual / out-of-band tenant context. Use for:
/// <list type="bullet">
///   <item>Background jobs that operate within a known tenant (e.g. scheduled per-tenant reports)</item>
///   <item>The first-run seeder that bootstraps the default tenant</item>
///   <item>Tests</item>
/// </list>
/// Construct with <c>null</c> for genuinely anonymous operations (signup, etc.) — EF filters
/// will return empty results, which is safe-by-default.
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
