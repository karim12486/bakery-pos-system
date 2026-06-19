using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Floor-plan editor + read endpoints. Gated to the <c>tables</c> feature (Pro by default).
/// Read endpoints open to any authenticated user; writes require ManageUsers (admin) since
/// reorganising the floor is an owner-level action, not a cashier action.
/// </summary>
[Route("api/branches/{branchId:int}/floor-plan")]
[ApiController]
[Authorize]
[RequiresFeature("tables")]
public class FloorPlanController : ControllerBase
{
    private readonly ITableService _tables;

    public FloorPlanController(ITableService tables)
    {
        _tables = tables;
    }

    /// <summary>Returns the entire floor plan for a branch in one payload —
    /// areas + their active tables. Powers the editor and the seat picker.</summary>
    [HttpGet]
    public async Task<ActionResult<FloorPlanDto>> Get(int branchId, CancellationToken ct)
        => Ok(await _tables.GetFloorPlanAsync(branchId, ct));
}

/// <summary>Area CRUD. Areas are logical sections of a branch's floor plan
/// (Indoor, Outdoor, Bar, Terrace, Private Room).</summary>
[Route("api/areas")]
[ApiController]
[Authorize]
[RequiresFeature("tables")]
public class AreasController : ControllerBase
{
    private readonly IAreaService _areas;

    public AreasController(IAreaService areas)
    {
        _areas = areas;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AreaDto>>> List(
        [FromQuery, Required] int branchId,
        [FromQuery] bool includeInactive,
        CancellationToken ct)
        => Ok(await _areas.ListForBranchAsync(branchId, includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AreaDto>> Get(int id, CancellationToken ct)
    {
        var area = await _areas.GetAsync(id, ct);
        return area == null ? NotFound() : Ok(area);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<AreaDto>> Create(AreaForCreateDto dto, CancellationToken ct)
    {
        var created = await _areas.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<AreaDto>> Update(int id, AreaForUpdateDto dto, CancellationToken ct)
        => Ok(await _areas.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _areas.DeactivateAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Table CRUD. Tables carry their floor-plan coordinates so the editor
/// can render the layout exactly as the owner positioned them.</summary>
[Route("api/tables")]
[ApiController]
[Authorize]
[RequiresFeature("tables")]
public class TablesController : ControllerBase
{
    private readonly ITableService _tables;

    public TablesController(ITableService tables)
    {
        _tables = tables;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TableDto>>> List(
        [FromQuery, Required] int branchId,
        [FromQuery] bool includeInactive,
        CancellationToken ct)
        => Ok(await _tables.ListForBranchAsync(branchId, includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TableDto>> Get(int id, CancellationToken ct)
    {
        var table = await _tables.GetAsync(id, ct);
        return table == null ? NotFound() : Ok(table);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<TableDto>> Create(TableForCreateDto dto, CancellationToken ct)
    {
        var created = await _tables.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<TableDto>> Update(int id, TableForUpdateDto dto, CancellationToken ct)
        => Ok(await _tables.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _tables.DeactivateAsync(id, ct);
        return NoContent();
    }
}
