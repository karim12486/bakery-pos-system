using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Modifier group catalogue management. Gated to plans that grant the <c>modifiers</c> feature
/// (Growth + Pro by default). Starter tenants get 403 with <c>requiredFeature=modifiers</c>
/// in the ProblemDetails extensions so the UI can render an upgrade CTA.
///
/// <para>Permission model: read operations are open to any authenticated user; write
/// operations require <c>ManageProducts</c> (since modifier groups attach to products and
/// share the catalogue-management permission).</para>
/// </summary>
[Route("api/modifier-groups")]
[ApiController]
[Authorize]
[RequiresFeature("modifiers")]
public class ModifierGroupsController : ControllerBase
{
    private readonly IModifierGroupService _service;

    public ModifierGroupsController(IModifierGroupService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModifierGroupDto>>> List(
        [FromQuery] bool includeInactive,
        CancellationToken ct)
        => Ok(await _service.ListAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ModifierGroupDto>> Get(int id, CancellationToken ct)
    {
        var group = await _service.GetAsync(id, ct);
        return group == null ? NotFound() : Ok(group);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<ModifierGroupDto>> Create(ModifierGroupForCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<ModifierGroupDto>> Update(int id, ModifierGroupForUpdateDto dto,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _service.DeactivateAsync(id, ct);
        return NoContent();
    }

    // ----- Modifiers within a group --------------------------------------------------

    [HttpPost("{groupId:int}/modifiers")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<ModifierDto>> AddModifier(int groupId, ModifierForCreateDto dto,
        CancellationToken ct)
    {
        var modifier = await _service.AddModifierAsync(groupId, dto, ct);
        return CreatedAtAction(nameof(Get), new { id = groupId }, modifier);
    }

    [HttpPut("{groupId:int}/modifiers/{modifierId:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<ModifierDto>> UpdateModifier(int groupId, int modifierId,
        ModifierForUpdateDto dto, CancellationToken ct)
        => Ok(await _service.UpdateModifierAsync(groupId, modifierId, dto, ct));

    [HttpDelete("{groupId:int}/modifiers/{modifierId:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> DeactivateModifier(int groupId, int modifierId, CancellationToken ct)
    {
        await _service.DeactivateModifierAsync(groupId, modifierId, ct);
        return NoContent();
    }
}

/// <summary>
/// Product-side endpoints for managing which modifier groups apply to a given product.
/// Kept on a separate controller so the routing is intuitive (<c>/api/products/{id}/modifier-groups</c>)
/// without bloating ProductsController.
/// </summary>
[Route("api/products/{productId:int}/modifier-groups")]
[ApiController]
[Authorize]
[RequiresFeature("modifiers")]
public class ProductModifierGroupsController : ControllerBase
{
    private readonly IModifierGroupService _service;

    public ProductModifierGroupsController(IModifierGroupService service)
    {
        _service = service;
    }

    /// <summary>Lists the modifier groups attached to a product, ordered by per-product sort.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModifierGroupDto>>> List(int productId, CancellationToken ct)
        => Ok(await _service.ListForProductAsync(productId, ct));

    /// <summary>Attach a modifier group to a product. Idempotent — re-attaching updates sort order only.</summary>
    [HttpPost]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> Attach(int productId, ProductModifierGroupAttachDto dto, CancellationToken ct)
    {
        await _service.AttachToProductAsync(productId, dto, ct);
        return NoContent();
    }

    /// <summary>Detach a modifier group from a product. Idempotent — no-op if already detached.</summary>
    [HttpDelete("{groupId:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> Detach(int productId, int groupId, CancellationToken ct)
    {
        await _service.DetachFromProductAsync(productId, groupId, ct);
        return NoContent();
    }
}
