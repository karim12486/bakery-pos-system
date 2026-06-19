using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Kitchen station management + category→station routing. Gated to the <c>kds</c> feature
/// (Pro / Restaurant Pack). Writes require ManageProducts since routing is a menu-config action.
/// </summary>
[Route("api/kitchen-stations")]
[ApiController]
[Authorize]
[RequiresFeature("kds")]
public class KitchenStationsController : ControllerBase
{
    private readonly IKitchenStationService _stations;

    public KitchenStationsController(IKitchenStationService stations)
    {
        _stations = stations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KitchenStationDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _stations.ListAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<KitchenStationDto>> Get(int id, CancellationToken ct)
    {
        var station = await _stations.GetAsync(id, ct);
        return station == null ? NotFound() : Ok(station);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<KitchenStationDto>> Create(KitchenStationForCreateDto dto, CancellationToken ct)
    {
        var created = await _stations.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<KitchenStationDto>> Update(int id, KitchenStationForUpdateDto dto, CancellationToken ct)
        => Ok(await _stations.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _stations.DeactivateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Route a category's products to a station (or clear with null).</summary>
    [HttpPut("/api/categories/{categoryId:int}/kitchen-station")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> RouteCategory(int categoryId, CategoryRouteDto dto, CancellationToken ct)
    {
        await _stations.RouteCategoryAsync(categoryId, dto, ct);
        return NoContent();
    }
}
