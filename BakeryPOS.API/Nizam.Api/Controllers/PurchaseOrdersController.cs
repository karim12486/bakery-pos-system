using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Purchase orders to suppliers (Phase 3.7). Draft → Submit → Receive (stock in) / Cancel.
/// Inventory Pack add-on — gated to <c>inventory_ops</c>; requires ManageInventory.
/// </summary>
[Route("api/purchase-orders")]
[ApiController]
[Authorize]
[RequiresFeature("inventory_ops")]
[HasPermission(UserPermissions.ManageInventory)]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrdersController(IPurchaseOrderService service)
    {
        _service = service;
    }

    private string Username => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderDto>>> List(
        [FromQuery] PurchaseOrderStatus? status, CancellationToken ct)
        => Ok(await _service.ListAsync(status, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> Get(int id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDto>> Create(PurchaseOrderCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, Username, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(int id, CancellationToken ct)
        => Ok(await _service.SubmitAsync(id, ct));

    [HttpPost("{id:int}/receive")]
    public async Task<ActionResult<PurchaseOrderDto>> Receive(int id, PurchaseOrderReceiveDto dto, CancellationToken ct)
        => Ok(await _service.ReceiveAsync(id, dto, Username, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(int id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));
}
