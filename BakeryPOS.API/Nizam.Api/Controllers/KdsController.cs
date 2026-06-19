using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services.Kds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Kitchen Display System — the queue read + the fire/cooking/ready/served/void actions.
/// Gated to the <c>kds</c> feature (Pro / Restaurant Pack). Actions push live updates to the
/// relevant station's SignalR group (see <see cref="Hubs.KdsHub"/>).
///
/// <para>Requires <see cref="UserPermissions.ProcessSales"/> — kitchen staff and servers
/// share that front-of-house permission. Voids additionally write an audit entry.</para>
/// </summary>
[Route("api/kds")]
[ApiController]
[Authorize]
[RequiresFeature("kds")]
[HasPermission(UserPermissions.ProcessSales)]
public class KdsController : ControllerBase
{
    private readonly IKdsService _kds;

    public KdsController(IKdsService kds)
    {
        _kds = kds;
    }

    /// <summary>Open items at a station for a branch, oldest-fired first, with aging colors.</summary>
    [HttpGet("stations/{stationId:int}/queue")]
    public async Task<ActionResult<IReadOnlyList<KdsItemDto>>> Queue(
        int stationId, [FromQuery] int branchId, CancellationToken ct)
        => Ok(await _kds.GetQueueAsync(branchId, stationId, ct));

    [HttpPost("items/{orderItemId:int}/fire")]
    public async Task<ActionResult<KdsItemDto>> Fire(int orderItemId, CancellationToken ct)
        => Ok(await _kds.FireAsync(orderItemId, ct));

    [HttpPost("items/{orderItemId:int}/cooking")]
    public async Task<ActionResult<KdsItemDto>> Cooking(int orderItemId, CancellationToken ct)
        => Ok(await _kds.MarkCookingAsync(orderItemId, ct));

    [HttpPost("items/{orderItemId:int}/ready")]
    public async Task<ActionResult<KdsItemDto>> Ready(int orderItemId, CancellationToken ct)
        => Ok(await _kds.MarkReadyAsync(orderItemId, ct));

    [HttpPost("items/{orderItemId:int}/served")]
    public async Task<ActionResult<KdsItemDto>> Served(int orderItemId, CancellationToken ct)
        => Ok(await _kds.MarkServedAsync(orderItemId, ct));

    [HttpPost("items/{orderItemId:int}/void")]
    public async Task<ActionResult<KdsItemDto>> Void(int orderItemId, VoidKdsItemDto dto, CancellationToken ct)
        => Ok(await _kds.VoidAsync(orderItemId, dto.Reason, ct));
}
