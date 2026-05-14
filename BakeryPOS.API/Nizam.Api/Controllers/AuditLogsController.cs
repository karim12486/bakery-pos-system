using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/audit-logs")]
[ApiController]
[Authorize]
[HasPermission(UserPermissions.ManageUsers)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditLogsController(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>
    /// Paginated audit log. Filterable by action (e.g. <c>sale.refund</c>), entity, user, time range.
    /// Tenant-admin only — audit trails are sensitive for staff and dispute investigations.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> List(
        [FromQuery] AuditLogQuery query,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
        => Ok(await _audit.ListAsync(query, pagination, ct));
}
