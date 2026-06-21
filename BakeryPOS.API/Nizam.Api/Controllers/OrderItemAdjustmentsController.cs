using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Void / comp adjustments on individual order items. Front-of-house actions (ProcessSales);
/// voiding a fired item or comping anything escalates to a manager-PIN override inline.
/// </summary>
[Route("api/order-items")]
[ApiController]
[Authorize]
[HasPermission(UserPermissions.ProcessSales)]
public class OrderItemAdjustmentsController : ControllerBase
{
    private readonly IOrderAdjustmentService _adjust;

    public OrderItemAdjustmentsController(IOrderAdjustmentService adjust)
    {
        _adjust = adjust;
    }

    /// <summary>Void an item. Manager PIN required if it's already fired.</summary>
    [HttpPost("{orderItemId:int}/void")]
    public async Task<ActionResult> Void(int orderItemId, VoidItemDto dto, CancellationToken ct)
    {
        await _adjust.VoidItemAsync(orderItemId, dto, ct);
        return NoContent();
    }

    /// <summary>Comp an item (free it). Manager PIN always required.</summary>
    [HttpPost("{orderItemId:int}/comp")]
    public async Task<ActionResult> Comp(int orderItemId, CompItemDto dto, CancellationToken ct)
    {
        await _adjust.CompItemAsync(orderItemId, dto, ct);
        return NoContent();
    }
}
