using Nizam.Api.Core.Attributes;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Nizam.Api.Services.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nizam.Api.Controllers;

/// <summary>
/// Platform operations for the NIZAM team. Login is anonymous + rate-limited; everything else
/// requires a super-admin token ([SuperAdminOnly]). Cross-tenant by design — these endpoints
/// onboard and manage every tenant.
/// </summary>
[Route("api/super-admin")]
[ApiController]
public class SuperAdminController : ControllerBase
{
    private readonly ISuperAdminService _super;
    private readonly ITenantFeatureOverrideService _overrides;

    public SuperAdminController(ISuperAdminService super, ITenantFeatureOverrideService overrides)
    {
        _super = super;
        _overrides = overrides;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SuperAdminTokenDto>> Login(SuperAdminLoginDto dto, CancellationToken ct)
        => Ok(await _super.LoginAsync(dto, ct));

    [HttpGet("tenants")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult<IReadOnlyList<TenantSummaryDto>>> ListTenants(
        [FromQuery] string? search, CancellationToken ct)
        => Ok(await _super.ListTenantsAsync(search, ct));

    [HttpGet("tenants/{tenantId:int}")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult<TenantDetailDto>> GetTenant(int tenantId, CancellationToken ct)
        => Ok(await _super.GetTenantAsync(tenantId, ct));

    [HttpPost("tenants/{tenantId:int}/suspend")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult> Suspend(int tenantId, CancellationToken ct)
    {
        await _super.SetStatusAsync(tenantId, "suspended", ct);
        return NoContent();
    }

    [HttpPost("tenants/{tenantId:int}/reactivate")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult> Reactivate(int tenantId, CancellationToken ct)
    {
        await _super.SetStatusAsync(tenantId, "active", ct);
        return NoContent();
    }

    [HttpPut("tenants/{tenantId:int}/plan")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult> ChangePlan(int tenantId, ChangePlanDto dto, CancellationToken ct)
    {
        await _super.ChangePlanAsync(tenantId, dto.PlanCode, ct);
        return NoContent();
    }

    // ----- Feature overrides (add-on grants / clawbacks) -----------------------------

    [HttpPut("tenants/{tenantId:int}/feature-overrides")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult> SetOverride(int tenantId, SetFeatureOverrideDto dto, CancellationToken ct)
    {
        await _overrides.SetAsync(tenantId, dto.FeatureKey, dto.Enabled, dto.ExpiresAt, dto.Reason, ct);
        return NoContent();
    }

    [HttpDelete("tenants/{tenantId:int}/feature-overrides/{featureKey}")]
    [Authorize, SuperAdminOnly]
    public async Task<ActionResult> ClearOverride(int tenantId, string featureKey, CancellationToken ct)
    {
        await _overrides.ClearAsync(tenantId, featureKey, ct);
        return NoContent();
    }
}
