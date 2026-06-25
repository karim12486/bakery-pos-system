using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Loyalty program config + customer point wallets. Gated to the <c>loyalty</c> feature
/// (native on Growth+, add-on on Starter). Program config + adjustments require ManageCustomers.
/// </summary>
[Route("api/loyalty")]
[ApiController]
[Authorize]
[RequiresFeature("loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyService _loyalty;

    public LoyaltyController(ILoyaltyService loyalty)
    {
        _loyalty = loyalty;
    }

    /// <summary>The tenant's loyalty program settings (earn/redeem rates, minimum).</summary>
    [HttpGet("program")]
    public async Task<ActionResult<LoyaltyProgramDto>> GetProgram(CancellationToken ct)
        => Ok(await _loyalty.GetProgramAsync(ct));

    [HttpPut("program")]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<ActionResult<LoyaltyProgramDto>> UpdateProgram(LoyaltyProgramUpdateDto dto, CancellationToken ct)
        => Ok(await _loyalty.UpdateProgramAsync(dto, ct));

    /// <summary>A customer's points balance + recent statement.</summary>
    [HttpGet("accounts/{customerId:int}")]
    public async Task<ActionResult<LoyaltyAccountDto>> GetAccount(int customerId, CancellationToken ct)
        => Ok(await _loyalty.GetAccountAsync(customerId, ct));

    /// <summary>Manual staff adjustment (goodwill / correction). Signed points.</summary>
    [HttpPost("adjust")]
    [HasPermission(UserPermissions.ManageCustomers)]
    public async Task<ActionResult<LoyaltyAccountDto>> Adjust(LoyaltyAdjustDto dto, CancellationToken ct)
        => Ok(await _loyalty.AdjustAsync(dto.CustomerId, dto.Points, dto.Reason, ct));

    /// <summary>Redeem a customer's points for a currency value (to apply as a discount).
    /// Deducts the points and returns the value + remaining balance.</summary>
    [HttpPost("accounts/{customerId:int}/redeem")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<LoyaltyRedeemResultDto>> Redeem(int customerId, [FromQuery] int points, CancellationToken ct)
        => Ok(await _loyalty.RedeemAsync(customerId, points, null, ct));
}
