using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Per-tenant messaging config (Telegram / WhatsApp) for outbound notifications — daily
/// reports, reservation reminders, etc. Gated to the <c>messaging_notifications</c> feature
/// (add-on on Starter, native on Growth+). Reads are tenant-admin; writes require ManageUsers.
/// </summary>
[Route("api/messaging-config")]
[ApiController]
[Authorize]
[RequiresFeature("messaging_notifications")]
public class MessagingConfigController : ControllerBase
{
    private readonly IMessagingConfigService _service;

    public MessagingConfigController(IMessagingConfigService service)
    {
        _service = service;
    }

    /// <summary>The tenant's current messaging config (secrets masked).</summary>
    [HttpGet]
    public async Task<ActionResult<MessagingConfigDto>> Get(CancellationToken ct)
        => Ok(await _service.GetAsync(ct));

    /// <summary>Creates or updates the tenant's messaging config.</summary>
    [HttpPut]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<MessagingConfigDto>> Update(MessagingConfigUpdateDto dto, CancellationToken ct)
        => Ok(await _service.UpdateAsync(dto, ct));

    /// <summary>Sends a test notification through the configured channel.</summary>
    [HttpPost("test")]
    [HasPermission(UserPermissions.ManageUsers)]
    public async Task<ActionResult<MessagingTestResultDto>> SendTest(CancellationToken ct)
        => Ok(await _service.SendTestAsync(ct));
}
