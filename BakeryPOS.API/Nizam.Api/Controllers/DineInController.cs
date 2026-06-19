using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Dine-in service operations — seat guests, view occupied tables, transfer, and close/clear.
/// Gated to the <c>tables</c> feature (Pro). Requires <see cref="UserPermissions.ProcessSales"/>
/// since these are front-of-house actions performed by servers/cashiers.
/// </summary>
[Route("api/dine-in")]
[ApiController]
[Authorize]
[RequiresFeature("tables")]
public class DineInController : ControllerBase
{
    private readonly IDineInService _dineIn;

    public DineInController(IDineInService dineIn)
    {
        _dineIn = dineIn;
    }

    /// <summary>Seat guests at a free table — opens a session + an Open dine-in order.</summary>
    [HttpPost("seat")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<TableSessionDto>> Seat(SeatGuestsDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException();
        var session = await _dineIn.SeatAsync(dto, username, ct);
        return CreatedAtAction(nameof(GetForTable), new { tableId = session.TableId }, session);
    }

    /// <summary>Open sessions (occupied tables) for a branch.</summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<TableSessionDto>>> ListOpen(
        [FromQuery, Required] int branchId, CancellationToken ct)
        => Ok(await _dineIn.ListOpenForBranchAsync(branchId, ct));

    /// <summary>The open session for a table, or 404 if the table is free.</summary>
    [HttpGet("tables/{tableId:int}/session")]
    public async Task<ActionResult<TableSessionDto>> GetForTable(int tableId, CancellationToken ct)
    {
        var session = await _dineIn.GetOpenForTableAsync(tableId, ct);
        return session == null ? NotFound() : Ok(session);
    }

    /// <summary>Transfer an open session (and its order) to a different free table.</summary>
    [HttpPost("sessions/{sessionId:int}/transfer")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult<TableSessionDto>> Transfer(int sessionId, TransferTableDto dto, CancellationToken ct)
        => Ok(await _dineIn.TransferAsync(sessionId, dto, ct));

    /// <summary>Close a session (guests gone / bill settled). Table goes Dirty until cleared.</summary>
    [HttpPost("sessions/{sessionId:int}/close")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult> Close(int sessionId, CancellationToken ct)
    {
        await _dineIn.CloseAsync(sessionId, ct);
        return NoContent();
    }

    /// <summary>Mark a Dirty table clean and Free for the next guests.</summary>
    [HttpPost("tables/{tableId:int}/clear")]
    [HasPermission(UserPermissions.ProcessSales)]
    public async Task<ActionResult> Clear(int tableId, CancellationToken ct)
    {
        await _dineIn.ClearTableAsync(tableId, ct);
        return NoContent();
    }
}
