using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Hubs;
using Nizam.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services.Kds;

public interface IKdsService
{
    /// <summary>Open kitchen items (Fired/Cooking/Ready) for a station at a branch, oldest first.</summary>
    Task<IReadOnlyList<KdsItemDto>> GetQueueAsync(int branchId, int stationId, CancellationToken ct);

    Task<KdsItemDto> FireAsync(int orderItemId, CancellationToken ct);
    Task<KdsItemDto> MarkCookingAsync(int orderItemId, CancellationToken ct);
    Task<KdsItemDto> MarkReadyAsync(int orderItemId, CancellationToken ct);
    Task<KdsItemDto> MarkServedAsync(int orderItemId, CancellationToken ct);
    Task<KdsItemDto> VoidAsync(int orderItemId, string reason, CancellationToken ct);
}

public sealed class KdsService : IKdsService
{
    // Aging thresholds (seconds). Tunable per-tenant later; constants for v1.
    private const int YellowAfterSeconds = 5 * 60;
    private const int RedAfterSeconds = 10 * 60;

    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHubContext<KdsHub> _hub;
    private readonly IAuditService _audit;

    public KdsService(
        AppDbContext context,
        ICurrentTenant currentTenant,
        IHubContext<KdsHub> hub,
        IAuditService audit)
    {
        _context = context;
        _currentTenant = currentTenant;
        _hub = hub;
        _audit = audit;
    }

    public async Task<IReadOnlyList<KdsItemDto>> GetQueueAsync(int branchId, int stationId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var items = await OpenItemsQuery()
            .Where(oi => oi.KitchenStationId == stationId && oi.Order!.BranchId == branchId)
            .OrderBy(oi => oi.FiredAt).ThenBy(oi => oi.Id)
            .ToListAsync(ct);

        return items.Select(oi => ToDto(oi, now)).ToList();
    }

    public Task<KdsItemDto> FireAsync(int orderItemId, CancellationToken ct)
        => TransitionAsync(orderItemId, OrderItemStatus.Fired, KdsEvents.ItemFired, ct);

    public Task<KdsItemDto> MarkCookingAsync(int orderItemId, CancellationToken ct)
        => TransitionAsync(orderItemId, OrderItemStatus.Cooking, KdsEvents.ItemCooking, ct);

    public Task<KdsItemDto> MarkReadyAsync(int orderItemId, CancellationToken ct)
        => TransitionAsync(orderItemId, OrderItemStatus.Ready, KdsEvents.ItemReady, ct);

    public Task<KdsItemDto> MarkServedAsync(int orderItemId, CancellationToken ct)
        => TransitionAsync(orderItemId, OrderItemStatus.Served, KdsEvents.ItemServed, ct);

    public async Task<KdsItemDto> VoidAsync(int orderItemId, string reason, CancellationToken ct)
    {
        var item = await LoadItemAsync(orderItemId, ct);
        AssertTransition(item, OrderItemStatus.Voided);

        item.Status = OrderItemStatus.Voided;
        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditActions.OrderItemVoided, "OrderItem", item.Id,
            $"order={item.OrderId};reason={reason}", ct: ct);

        return await BroadcastAsync(item, KdsEvents.ItemVoided, ct);
    }

    // ----- Internals ----------------------------------------------------------------

    private IQueryable<OrderItem> OpenItemsQuery() => _context.OrderItems
        .Include(oi => oi.Product)
        .Include(oi => oi.AppliedModifiers)
        .Include(oi => oi.Order)
        .Where(oi => oi.KitchenStationId != null
                     && (oi.Status == OrderItemStatus.Fired
                         || oi.Status == OrderItemStatus.Cooking
                         || oi.Status == OrderItemStatus.Ready));

    private async Task<OrderItem> LoadItemAsync(int orderItemId, CancellationToken ct)
        => await _context.OrderItems
               .Include(oi => oi.Product)
               .Include(oi => oi.AppliedModifiers)
               .Include(oi => oi.Order)
               .FirstOrDefaultAsync(oi => oi.Id == orderItemId, ct)
           ?? throw new DomainNotFoundException("ERR_ORDER_ITEM_NOT_FOUND",
               $"Order item {orderItemId} introuvable.");

    private async Task<KdsItemDto> TransitionAsync(
        int orderItemId, OrderItemStatus to, string evt, CancellationToken ct)
    {
        var item = await LoadItemAsync(orderItemId, ct);
        AssertTransition(item, to);

        item.Status = to;
        if (to == OrderItemStatus.Fired && item.FiredAt == null) item.FiredAt = DateTime.UtcNow;
        if (to == OrderItemStatus.Served) item.ServedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return await BroadcastAsync(item, evt, ct);
    }

    private static void AssertTransition(OrderItem item, OrderItemStatus to)
    {
        if (!KdsItemStateMachine.CanTransition(item.Status, to))
            throw new DomainException("ERR_INVALID_KDS_TRANSITION",
                $"Kitchen item cannot move from '{item.Status}' to '{to}'.",
                StatusCodes.Status422UnprocessableEntity,
                new Dictionary<string, object?> { ["fromStatus"] = item.Status.ToString(), ["toStatus"] = to.ToString() });
    }

    private async Task<KdsItemDto> BroadcastAsync(OrderItem item, string evt, CancellationToken ct)
    {
        var dto = ToDto(item, DateTime.UtcNow);

        // Broadcast to the (tenant, branch, station) group so every screen updates live.
        if (_currentTenant.TenantId is int tenantId
            && item.Order?.BranchId is int branchId
            && item.KitchenStationId is int stationId)
        {
            await _hub.Clients
                .Group(KdsHub.StationGroup(tenantId, branchId, stationId))
                .SendAsync(evt, dto, ct);
        }

        return dto;
    }

    private static KdsItemDto ToDto(OrderItem oi, DateTime now)
    {
        var ageSeconds = oi.FiredAt.HasValue ? (int)Math.Max(0, (now - oi.FiredAt.Value).TotalSeconds) : 0;
        return new KdsItemDto
        {
            OrderItemId = oi.Id,
            OrderId = oi.OrderId,
            TableId = oi.Order?.TableId,
            KitchenStationId = oi.KitchenStationId,
            ProductName = oi.Product?.Name ?? "Unknown",
            Quantity = oi.Quantity,
            Status = oi.Status.ToString(),
            Modifiers = oi.AppliedModifiers.Select(m => m.Name).ToList(),
            Notes = oi.Notes,
            FiredAt = oi.FiredAt,
            AgeSeconds = ageSeconds,
            AgeColor = ageSeconds >= RedAfterSeconds ? "red"
                     : ageSeconds >= YellowAfterSeconds ? "yellow"
                     : "green",
        };
    }
}
