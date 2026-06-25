using System.Security.Claims;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Stock waste log (Phase 3.7). Recording an entry decrements stock and writes a Waste
/// movement. Inventory Pack add-on — gated to <c>inventory_ops</c>; requires ManageInventory.
/// </summary>
[Route("api/waste-log")]
[ApiController]
[Authorize]
[RequiresFeature("inventory_ops")]
[HasPermission(UserPermissions.ManageInventory)]
public class WasteLogController : ControllerBase
{
    private readonly IWasteLogService _service;

    public WasteLogController(IWasteLogService service)
    {
        _service = service;
    }

    private string Username => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WasteLogEntryDto>>> List(
        [FromQuery] int? productId, CancellationToken ct)
        => Ok(await _service.ListAsync(productId, ct));

    [HttpPost]
    public async Task<ActionResult<WasteLogEntryDto>> Record(WasteLogCreateDto dto, CancellationToken ct)
        => Ok(await _service.RecordAsync(dto, Username, ct));
}
