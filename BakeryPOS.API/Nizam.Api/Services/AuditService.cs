using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IAuditService
{
    Task LogAsync(string action, string? entityType = null, int? entityId = null,
        string? details = null, CancellationToken ct = default);

    Task<PagedResponse<AuditLogDto>> ListAsync(AuditLogQuery query, PaginationParams pagination, CancellationToken ct);
}

/// <summary>
/// Standard action codes — keep these stable so dashboards and alerts can switch on them.
/// New codes are fine; never repurpose an existing one for a different meaning.
/// </summary>
public static class AuditActions
{
    public const string SaleCreated = "sale.create";
    public const string SaleRefunded = "sale.refund";
    public const string ProductPriceChanged = "product.price_changed";
    public const string ProductDeleted = "product.delete";
    public const string UserCreated = "user.create";
    public const string UserPermissionsChanged = "user.permissions_changed";
    public const string UserDeactivated = "user.deactivate";
    public const string ShiftOpened = "shift.open";
    public const string ShiftClosedWithVariance = "shift.variance";
    public const string RemovalApproved = "removal.approve";
    public const string RemovalRejected = "removal.reject";
    public const string LoginFailed = "login.failed";
    public const string SettingsChanged = "settings.changed";
    public const string OrderItemVoided = "order_item.void";
}

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpContextAccessor _httpContext;

    public AuditService(AppDbContext context, ICurrentTenant currentTenant, IHttpContextAccessor httpContext)
    {
        _context = context;
        _currentTenant = currentTenant;
        _httpContext = httpContext;
    }

    public async Task LogAsync(string action, string? entityType = null, int? entityId = null,
        string? details = null, CancellationToken ct = default)
    {
        var http = _httpContext.HttpContext;

        // Username from claim — falls back to "system" for hosted services / seeder paths.
        var username = http?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userIdStr = http?.User?.FindFirst("uid")?.Value;
        int? userId = int.TryParse(userIdStr, out var uid) ? uid : null;

        _context.AuditLogs.Add(new AuditLog
        {
            BranchId = _currentTenant.BranchId,
            UserId = userId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            At = DateTime.UtcNow
            // TenantId auto-stamped
        });

        // Persist immediately — callers don't need to remember to SaveChanges.
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PagedResponse<AuditLogDto>> ListAsync(AuditLogQuery query, PaginationParams pagination, CancellationToken ct)
    {
        var q = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(query.Action)) q = q.Where(x => x.Action == query.Action);
        if (!string.IsNullOrEmpty(query.EntityType)) q = q.Where(x => x.EntityType == query.EntityType);
        if (query.EntityId.HasValue) q = q.Where(x => x.EntityId == query.EntityId.Value);
        if (query.UserId.HasValue) q = q.Where(x => x.UserId == query.UserId.Value);
        if (query.Since.HasValue) q = q.Where(x => x.At >= query.Since.Value);
        if (query.Until.HasValue) q = q.Where(x => x.At < query.Until.Value);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(x => x.At)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                BranchId = x.BranchId,
                UserId = x.UserId,
                Username = x.Username,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                IpAddress = x.IpAddress,
                At = x.At
            })
            .ToListAsync(ct);

        return new PagedResponse<AuditLogDto>(rows, pagination.PageNumber, pagination.PageSize, total);
    }
}
