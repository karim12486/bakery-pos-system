using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Promotions management + preview. Gated to the <c>promotions</c> feature (Growth+). Writes
/// require ManageProducts (catalogue-level config).
/// </summary>
[Route("api/promotions")]
[ApiController]
[Authorize]
[RequiresFeature("promotions")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promos;

    public PromotionsController(IPromotionService promos)
    {
        _promos = promos;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> List([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _promos.ListAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PromotionDto>> Get(int id, CancellationToken ct)
    {
        var p = await _promos.GetAsync(id, ct);
        return p == null ? NotFound() : Ok(p);
    }

    [HttpPost]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<PromotionDto>> Create(PromotionForCreateDto dto, CancellationToken ct)
    {
        var created = await _promos.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult<PromotionDto>> Update(int id, PromotionForUpdateDto dto, CancellationToken ct)
        => Ok(await _promos.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(UserPermissions.ManageProducts)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken ct)
    {
        await _promos.DeactivateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Preview the discount a code (or auto-apply) yields for a subtotal — for the
    /// cashier to show before committing the sale.</summary>
    [HttpGet("evaluate")]
    public async Task<ActionResult<PromotionApplyResultDto?>> Evaluate(
        [FromQuery] string? code, [FromQuery] decimal subtotal, CancellationToken ct)
        => Ok(await _promos.EvaluateAsync(code, subtotal, ct));
}
