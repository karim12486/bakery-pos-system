using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

/// <summary>
/// Public, anonymous QR-code menu. A diner scans a table's QR (which encodes the tenant slug)
/// and gets the menu with no login. Gated by the <c>qr_table_menu</c> plan feature, checked
/// inside the service (this endpoint has no tenant/auth context for [RequiresFeature]).
///
/// <para>404 covers both "no such tenant" and "feature not on plan" so the endpoint doesn't
/// confirm which tenants exist.</para>
/// </summary>
[Route("api/public")]
[ApiController]
[AllowAnonymous]
public class PublicMenuController : ControllerBase
{
    private readonly IPublicMenuService _menu;

    public PublicMenuController(IPublicMenuService menu)
    {
        _menu = menu;
    }

    [HttpGet("{tenantSlug}/menu")]
    [ResponseCache(Duration = 60)] // a menu rarely changes minute-to-minute; cache at the edge
    public async Task<ActionResult<PublicMenuDto>> Get(string tenantSlug, CancellationToken ct)
        => Ok(await _menu.GetMenuAsync(tenantSlug, ct));
}
