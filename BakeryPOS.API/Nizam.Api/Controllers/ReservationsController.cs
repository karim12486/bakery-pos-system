using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Table reservations. Gated to the <c>tables</c> feature (Pro / Restaurant Pack). Writes
/// require <see cref="UserPermissions.ProcessSales"/> (front-of-house).
/// </summary>
[Route("api/reservations")]
[ApiController]
[Authorize]
[RequiresFeature("tables")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservations;

    public ReservationsController(IReservationService reservations)
    {
        _reservations = reservations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> List(
        [FromQuery, Required] int branchId, [FromQuery] DateTime? onDate, CancellationToken ct)
        => Ok(await _reservations.ListForBranchAsync(branchId, onDate, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationDto>> Get(int id, CancellationToken ct)
    {
        var r = await _reservations.GetAsync(id, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<ReservationDto>> Create(ReservationForCreateDto dto, CancellationToken ct)
    {
        var created = await _reservations.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/confirm")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<ReservationDto>> Confirm(int id, CancellationToken ct)
        => Ok(await _reservations.ConfirmAsync(id, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<ReservationDto>> Cancel(int id, CancellationToken ct)
        => Ok(await _reservations.CancelAsync(id, ct));

    [HttpPost("{id:int}/no-show")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<ReservationDto>> NoShow(int id, CancellationToken ct)
        => Ok(await _reservations.MarkNoShowAsync(id, ct));

    /// <summary>Seat the booked party now — opens a table session.</summary>
    [HttpPost("{id:int}/seat")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<TableSessionDto>> Seat(int id, SeatReservationDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _reservations.SeatAsync(id, dto, username, ct));
    }
}
