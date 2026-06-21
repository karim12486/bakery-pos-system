using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services.Kds;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IOrderAdjustmentService
{
    /// <summary>Voids an order item. If the item is past Pending (already fired), a valid
    /// manager PIN with ApproveRemovals is required. Recomputes the order total.</summary>
    Task VoidItemAsync(int orderItemId, VoidItemDto dto, CancellationToken ct);

    /// <summary>Comps an order item (zeroes its charge; kitchen still makes it). Always
    /// requires a manager override. Recomputes the order total.</summary>
    Task CompItemAsync(int orderItemId, CompItemDto dto, CancellationToken ct);
}

public sealed class OrderAdjustmentService : IOrderAdjustmentService
{
    private readonly AppDbContext _context;
    private readonly IPinAuthService _pinAuth;
    private readonly IKdsService _kds;
    private readonly IAuditService _audit;

    public OrderAdjustmentService(
        AppDbContext context, IPinAuthService pinAuth, IKdsService kds, IAuditService audit)
    {
        _context = context;
        _pinAuth = pinAuth;
        _kds = kds;
        _audit = audit;
    }

    public async Task VoidItemAsync(int orderItemId, VoidItemDto dto, CancellationToken ct)
    {
        var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.Id == orderItemId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_ITEM_NOT_FOUND", $"Order item {orderItemId} introuvable.");

        if (item.Status == OrderItemStatus.Voided)
            throw new DomainConflictException("ERR_ALREADY_VOIDED", "This item is already voided.");

        // Voiding a fired item needs a supervisor's PIN — front-of-house can't quietly drop
        // something the kitchen already started.
        var pastPending = item.Status is OrderItemStatus.Fired or OrderItemStatus.Cooking or OrderItemStatus.Ready;
        if (pastPending)
        {
            if (string.IsNullOrEmpty(dto.ManagerPin))
                throw new DomainException("ERR_OVERRIDE_REQUIRED",
                    "A manager PIN is required to void an item that's already been fired.",
                    StatusCodes.Status403Forbidden,
                    new Dictionary<string, object?> { ["requiresOverride"] = true });

            await _pinAuth.ValidateManagerPinAsync(dto.ManagerPin, UserPermissions.ApproveRemovals, ct);
        }

        // Delegate the actual void to the KDS service (transition + audit + station broadcast),
        // then recompute the order total here.
        await _kds.VoidAsync(orderItemId, dto.Reason, ct);
        await RecomputeOrderTotalAsync(item.OrderId, ct);
    }

    public async Task CompItemAsync(int orderItemId, CompItemDto dto, CancellationToken ct)
    {
        var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.Id == orderItemId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_ITEM_NOT_FOUND", $"Order item {orderItemId} introuvable.");

        if (item.Status == OrderItemStatus.Voided)
            throw new DomainConflictException("ERR_ITEM_VOIDED", "A voided item cannot be comped.");
        if (item.IsComped)
            throw new DomainConflictException("ERR_ALREADY_COMPED", "This item is already comped.");

        // Comping always needs a manager — it gives away money.
        var manager = await _pinAuth.ValidateManagerPinAsync(dto.ManagerPin, UserPermissions.ApproveRemovals, ct);

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        var compedAmount = item.LineTotal;
        item.IsComped = true;
        item.LineTotal = 0m; // kitchen still makes it; guest isn't charged

        await _context.SaveChangesAsync(ct);
        await RecomputeOrderTotalAsync(item.OrderId, ct);

        await _audit.LogAsync(AuditActions.OrderItemComped, "OrderItem", item.Id,
            $"order={item.OrderId};amount={compedAmount};manager={manager.Username};reason={dto.Reason}", ct: ct);

        await tx.CommitAsync(ct);
    }

    private async Task RecomputeOrderTotalAsync(int orderId, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) return;

        var subtotal = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId && oi.Status != OrderItemStatus.Voided)
            .SumAsync(oi => oi.LineTotal, ct);

        order.Subtotal = subtotal;
        order.FinalAmount = subtotal - order.DiscountAmount;
        await _context.SaveChangesAsync(ct);
    }
}
