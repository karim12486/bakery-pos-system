using System.Security.Claims;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shifts;

    public ShiftsController(IShiftService shifts)
    {
        _shifts = shifts;
    }

    /// <summary>
    /// Opens a new shift for the authenticated user. Step 2 of the "Open POS Shift" flow
    /// from the Figma — the cashier enters the opening cash float on the keypad. Refuses
    /// if the user already has an open shift somewhere.
    /// </summary>
    [HttpPost("open")]
    public async Task<ActionResult<ShiftDto>> OpenShift(OpenShiftDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        var shift = await _shifts.OpenAsync(dto, username, ct);
        return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift);
    }

    /// <summary>
    /// Closes the specified shift, computes variance, and returns the Z-report. Only the
    /// shift's owning user can close it.
    /// </summary>
    [HttpPost("{id:int}/close")]
    public async Task<ActionResult<ZReportDto>> CloseShift(int id, CloseShiftDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        var z = await _shifts.CloseAsync(id, dto, username, ct);
        return Ok(z);
    }

    /// <summary>The current user's open shift, if any. Used by the POS app on every load
    /// to decide whether to show the Cashier screen or the Open-Shift prompt.</summary>
    [HttpGet("current")]
    public async Task<ActionResult<ShiftDto>> GetCurrent(CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        var shift = await _shifts.GetCurrentAsync(username, ct);
        return shift == null ? NotFound() : Ok(shift);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShiftDto>> GetShift(int id, CancellationToken ct)
    {
        var shift = await _shifts.GetAsync(id, ct);
        return shift == null ? NotFound() : Ok(shift);
    }

    /// <summary>Re-fetch the Z-report for a closed shift (e.g. to reprint).</summary>
    [HttpGet("{id:int}/zreport")]
    public async Task<ActionResult<ZReportDto>> GetZReport(int id, CancellationToken ct)
    {
        var z = await _shifts.GetZReportAsync(id, ct);
        return z == null ? NotFound() : Ok(z);
    }
}
