using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryPOS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;

    public SettingsController(ISettingsService settings, IAuditService audit)
    {
        _settings = settings;
        _audit = audit;
    }

    /// <summary>Reads all well-known settings (tax rate, currency, business name, etc.).
    /// Frontend hits this on bootstrap. Anyone authenticated can read — settings aren't secret.</summary>
    [HttpGet]
    public async Task<ActionResult<TenantSettingsDto>> Get(CancellationToken ct)
        => Ok(await _settings.GetWellKnownAsync(ct));

    /// <summary>Raw key/value dump — useful for the admin Settings page.</summary>
    [HttpGet("raw")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<IReadOnlyDictionary<string, string>>> GetRaw(CancellationToken ct)
        => Ok(await _settings.GetAllAsync(ct));

    /// <summary>Upsert one or more settings. Tenant-admin only.</summary>
    [HttpPut]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<IActionResult> Upsert([FromBody] IEnumerable<SettingUpsertDto> dtos, CancellationToken ct)
    {
        var dict = dtos.ToDictionary(d => d.Key, d => d.Value);
        await _settings.SetManyAsync(dict, ct);
        await _audit.LogAsync(AuditActions.SettingsChanged, "Setting", details: string.Join(",", dict.Keys), ct: ct);
        return NoContent();
    }
}
