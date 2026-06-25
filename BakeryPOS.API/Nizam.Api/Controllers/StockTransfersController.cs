using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Branch-to-branch stock transfers (Phase 3.7). Draft → Send → Receive / Cancel.
/// Inventory Pack add-on — gated to <c>inventory_ops</c>; requires ManageInventory.
/// </summary>
[Route("api/stock-transfers")]
[ApiController]
[Authorize]
[RequiresFeature("inventory_ops")]
[HasPermission(UserPermissions.ManageInventory)]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _service;

    public StockTransfersController(IStockTransferService service)
    {
        _service = service;
    }

    private string Username => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockTransferDto>>> List(
        [FromQuery] StockTransferStatus? status, CancellationToken ct)
        => Ok(await _service.ListAsync(status, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StockTransferDto>> Get(int id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StockTransferDto>> Create(StockTransferCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, Username, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/send")]
    public async Task<ActionResult<StockTransferDto>> Send(int id, CancellationToken ct)
        => Ok(await _service.SendAsync(id, Username, ct));

    [HttpPost("{id:int}/receive")]
    public async Task<ActionResult<StockTransferDto>> Receive(int id, CancellationToken ct)
        => Ok(await _service.ReceiveAsync(id, Username, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<StockTransferDto>> Cancel(int id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));
}
