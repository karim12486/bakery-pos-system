using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Suppliers the tenant buys stock from (Phase 3.7). Part of the Inventory Pack add-on —
/// gated to <c>inventory_ops</c>. All actions require ManageInventory.
/// </summary>
[Route("api/suppliers")]
[ApiController]
[Authorize]
[RequiresFeature("inventory_ops")]
[HasPermission(UserPermissions.ManageInventory)]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> List([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _service.ListAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> Get(int id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(SupplierUpsertDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SupplierDto>> Update(int id, SupplierUpsertDto dto, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _service.DeactivateAsync(id, ct);
        return NoContent();
    }
}
