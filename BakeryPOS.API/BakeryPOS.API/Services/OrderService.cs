using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IOrderService
{
    /// <summary>Create an Order with Status=Open — a parked cart.</summary>
    Task<ParkedCartDetailDto> ParkAsync(ParkedCartDto dto, string username, CancellationToken ct);

    /// <summary>Replace the items + label on a parked cart. Status must still be Open.</summary>
    Task<ParkedCartDetailDto> UpdateParkedAsync(int orderId, ParkedCartDto dto, string username, CancellationToken ct);

    /// <summary>Lists the current user's parked carts (Status=Open).</summary>
    Task<IReadOnlyList<ParkedCartDetailDto>> ListParkedAsync(string username, CancellationToken ct);

    /// <summary>Discard a parked cart (Status: Open → Cancelled). Refuses if already closed.</summary>
    Task DiscardAsync(int orderId, string username, CancellationToken ct);

    Task<ParkedCartDetailDto?> GetAsync(int orderId, CancellationToken ct);
}

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _context;

    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ParkedCartDetailDto> ParkAsync(ParkedCartDto dto, string username, CancellationToken ct)
    {
        var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_CASHIER_NOT_FOUND", "Caissier introuvable.", StatusCodes.Status401Unauthorized);

        // Optional customer attachment (Premium client with a discount tier, or a tab-payment target).
        Customer? customer = null;
        if (dto.CustomerId.HasValue)
        {
            customer = await _context.Customers.FindAsync(new object?[] { dto.CustomerId.Value }, ct)
                ?? throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");
        }

        // Load all referenced products in one query.
        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = DateTime.UtcNow;
        var newOrder = new Order
        {
            CashierUserId = cashier.Id,
            CustomerId = dto.CustomerId,
            Status = OrderStatus.Open,
            Channel = OrderChannel.Takeaway,
            OpenedAt = now,
            Notes = dto.Name
            // Totals computed below; TenantId auto-stamped
        };

        decimal subtotal = 0;
        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new DomainException("ERR_PRODUCT_NOT_FOUND", $"Produit avec l'ID {item.ProductId} introuvable.");

            var line = product.Price * item.Quantity;
            subtotal += line;

            newOrder.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                LineTotal = line,
                Status = OrderItemStatus.Pending
            });
        }

        newOrder.Subtotal = subtotal;
        newOrder.FinalAmount = subtotal; // discount/tax applied at close time

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync(ct);

        return await BuildDetailDtoAsync(newOrder.Id, ct)
            ?? throw new InvalidOperationException("Order just saved was not retrievable.");
    }

    public async Task<ParkedCartDetailDto> UpdateParkedAsync(int orderId, ParkedCartDto dto, string username, CancellationToken ct)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Cashier)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_NOT_FOUND", "Panier introuvable.");

        if (order.Cashier?.Username != username)
            throw new DomainException("ERR_ORDER_NOT_OWNED",
                "Vous ne pouvez modifier que vos propres paniers.", StatusCodes.Status403Forbidden);

        if (order.Status != OrderStatus.Open)
            throw new DomainConflictException("ERR_ORDER_NOT_PARKED",
                "Ce panier n'est plus modifiable (déjà clôturé ou annulé).");

        // Replace items wholesale — simpler than per-line diff and matches the "drag and drop"
        // mental model of a draft cart.
        _context.OrderItems.RemoveRange(order.Items);

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        decimal subtotal = 0;
        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new DomainException("ERR_PRODUCT_NOT_FOUND", $"Produit avec l'ID {item.ProductId} introuvable.");

            var line = product.Price * item.Quantity;
            subtotal += line;

            _context.OrderItems.Add(new OrderItem
            {
                Order = order,
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                LineTotal = line,
                Status = OrderItemStatus.Pending
            });
        }

        order.Notes = dto.Name;
        order.CustomerId = dto.CustomerId;
        order.Subtotal = subtotal;
        order.FinalAmount = subtotal;

        await _context.SaveChangesAsync(ct);

        return await BuildDetailDtoAsync(order.Id, ct)
            ?? throw new InvalidOperationException("Order just updated was not retrievable.");
    }

    public async Task<IReadOnlyList<ParkedCartDetailDto>> ListParkedAsync(string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user == null) return Array.Empty<ParkedCartDetailDto>();

        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.CashierUserId == user.Id && o.Status == OrderStatus.Open)
            .OrderBy(o => o.OpenedAt)
            .ToListAsync(ct);

        return orders.Select(ToDto).ToList();
    }

    public async Task DiscardAsync(int orderId, string username, CancellationToken ct)
    {
        var order = await _context.Orders.Include(o => o.Cashier)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_NOT_FOUND", "Panier introuvable.");

        if (order.Cashier?.Username != username)
            throw new DomainException("ERR_ORDER_NOT_OWNED",
                "Vous ne pouvez annuler que vos propres paniers.", StatusCodes.Status403Forbidden);

        if (order.Status != OrderStatus.Open)
            throw new DomainConflictException("ERR_ORDER_NOT_PARKED",
                "Seuls les paniers ouverts peuvent être annulés.");

        order.Status = OrderStatus.Cancelled;
        order.ClosedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ParkedCartDetailDto?> GetAsync(int orderId, CancellationToken ct)
        => await BuildDetailDtoAsync(orderId, ct);

    private async Task<ParkedCartDetailDto?> BuildDetailDtoAsync(int orderId, CancellationToken ct)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        return order == null ? null : ToDto(order);
    }

    private static ParkedCartDetailDto ToDto(Order o) => new()
    {
        Id = o.Id,
        Name = o.Notes,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.Name,
        OpenedAt = o.OpenedAt,
        Subtotal = o.Subtotal,
        Items = o.Items.Select(i => new ParkedCartItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? "Unknown",
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.LineTotal
        }).ToList()
    };
}
