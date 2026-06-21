using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Split-bill checks for an order. Gated to the <c>split_check</c> feature (Pro / Restaurant
/// Pack). Mutations require <see cref="UserPermissions.ProcessSales"/> (front-of-house).
/// </summary>
[Route("api")]
[ApiController]
[Authorize]
[RequiresFeature("split_check")]
public class ChecksController : ControllerBase
{
    private readonly ICheckService _checks;

    public ChecksController(ICheckService checks)
    {
        _checks = checks;
    }

    /// <summary>List the checks for an order.</summary>
    [HttpGet("orders/{orderId:int}/checks")]
    public async Task<ActionResult<IReadOnlyList<CheckDto>>> List(int orderId, CancellationToken ct)
        => Ok(await _checks.GetForOrderAsync(orderId, ct));

    /// <summary>(Re)split an order into checks. Replaces existing unpaid checks.</summary>
    [HttpPost("orders/{orderId:int}/checks")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<IReadOnlyList<CheckDto>>> Create(int orderId, CreateChecksDto dto, CancellationToken ct)
        => Ok(await _checks.CreateChecksAsync(orderId, dto, ct));

    /// <summary>Toggle the automatic service charge on a check.</summary>
    [HttpPut("checks/{checkId:int}/service-charge")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<CheckDto>> SetServiceCharge(int checkId, SetServiceChargeDto dto, CancellationToken ct)
        => Ok(await _checks.SetServiceChargeAsync(checkId, dto.OptedOut, ct));

    /// <summary>Settle a check. Closes the order when its last open check is paid.</summary>
    [HttpPost("checks/{checkId:int}/pay")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<CheckDto>> Pay(int checkId, PayCheckDto dto, CancellationToken ct)
        => Ok(await _checks.PayAsync(checkId, dto.PaymentMethod, ct));
}
