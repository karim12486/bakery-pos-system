using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[HasPermission(UserPermissions.ProcessSales)]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>Park a new cart. Returns the saved Order with id so the client can resume.</summary>
    [HttpPost("park")]
    public async Task<ActionResult<ParkedCartDetailDto>> Park(ParkedCartDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        var cart = await _orders.ParkAsync(dto, username, ct);
        return CreatedAtAction(nameof(GetParked), new { id = cart.Id }, cart);
    }

    /// <summary>Lists THIS cashier's parked carts (no cross-cashier visibility).</summary>
    [HttpGet("parked")]
    public async Task<ActionResult<IEnumerable<ParkedCartDetailDto>>> ListParked(CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _orders.ListParkedAsync(username, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ParkedCartDetailDto>> GetParked(int id, CancellationToken ct)
    {
        var cart = await _orders.GetAsync(id, ct);
        return cart == null ? NotFound() : Ok(cart);
    }

    /// <summary>Replace items / label / customer on a parked cart. Status must still be Open.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ParkedCartDetailDto>> UpdateParked(int id, ParkedCartDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _orders.UpdateParkedAsync(id, dto, username, ct));
    }

    /// <summary>Discard a parked cart. Status moves to Cancelled. Only the owning cashier can discard.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Discard(int id, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        await _orders.DiscardAsync(id, username, ct);
        return NoContent();
    }
}
