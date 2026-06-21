using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services.Kds;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IDineInService
{
    /// <summary>Seats guests at a free table: opens a <see cref="TableSession"/>, creates an
    /// Open dine-in <see cref="Order"/>, and flips the table to Occupied.</summary>
    Task<TableSessionDto> SeatAsync(SeatGuestsDto dto, string actingUsername, CancellationToken ct);

    /// <summary>Lists open sessions (occupied tables) for a branch.</summary>
    Task<IReadOnlyList<TableSessionDto>> ListOpenForBranchAsync(int branchId, CancellationToken ct);

    /// <summary>The open session for a specific table, or null if the table is free.</summary>
    Task<TableSessionDto?> GetOpenForTableAsync(int tableId, CancellationToken ct);

    /// <summary>Moves an open session (and its order) to a different free table.</summary>
    Task<TableSessionDto> TransferAsync(int sessionId, TransferTableDto dto, CancellationToken ct);

    /// <summary>Merges <paramref name="sessionId"/> INTO <paramref name="intoSessionId"/>: moves
    /// the source order's items to the destination order, sums guest counts, closes the source
    /// session and frees its table (Dirty), cancels the now-empty source order. Both must be open,
    /// same branch, and neither order may have checks yet.</summary>
    Task<TableSessionDto> MergeAsync(int sessionId, int intoSessionId, CancellationToken ct);

    /// <summary>Closes the session (guests gone / bill settled) and flips the table to Dirty.</summary>
    Task CloseAsync(int sessionId, CancellationToken ct);

    /// <summary>Marks a Dirty table clean and Free, ready for the next guests.</summary>
    Task ClearTableAsync(int tableId, CancellationToken ct);

    /// <summary>The dine-in order with its current line items + running totals.</summary>
    Task<DineInOrderDto> GetOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Appends items (with modifiers) to an open dine-in order as Pending — not yet
    /// sent to the kitchen. Recomputes the order total. Validates modifiers + routes each item
    /// to its kitchen station.</summary>
    Task<DineInOrderDto> AddItemsAsync(int orderId, AddOrderItemsDto dto, CancellationToken ct);

    /// <summary>Fires all Pending items on the order to the kitchen (Pending → Fired), stamping
    /// FiredAt and broadcasting each to its KDS station screen.</summary>
    Task<DineInOrderDto> FireOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Fires only the Pending items in a given course — lets the server pace the meal
    /// (fire appetizers now, mains later).</summary>
    Task<DineInOrderDto> FireCourseAsync(int orderId, int courseNumber, CancellationToken ct);
}

public sealed class DineInService : IDineInService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IModifierApplicationService _modifierApp;
    private readonly IKdsService _kds;
    private readonly Orders.IOrderStateMachine _orderStates;

    public DineInService(
        AppDbContext context,
        IMapper mapper,
        IModifierApplicationService modifierApp,
        IKdsService kds,
        Orders.IOrderStateMachine orderStates)
    {
        _context = context;
        _mapper = mapper;
        _modifierApp = modifierApp;
        _kds = kds;
        _orderStates = orderStates;
    }

    public async Task<TableSessionDto> SeatAsync(SeatGuestsDto dto, string actingUsername, CancellationToken ct)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == dto.TableId && t.IsActive, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", $"Table {dto.TableId} introuvable.");

        // Reject seating onto an occupied/reserved table. The open-session check is the logical
        // guard; RowVersion on TableSession backs it up against races on SQL Server.
        var existingOpen = await _context.TableSessions
            .AnyAsync(s => s.TableId == table.Id && s.ClosedAt == null, ct);
        if (existingOpen || table.Status == TableStatus.Occupied)
            throw new DomainConflictException("ERR_TABLE_OCCUPIED",
                $"Table '{table.Name}' is already occupied.");

        var actingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == actingUsername, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        // Server defaults to the acting user unless explicitly assigned.
        var serverUserId = dto.ServerUserId ?? actingUser.Id;
        if (dto.ServerUserId.HasValue &&
            !await _context.Users.AnyAsync(u => u.Id == dto.ServerUserId.Value, ct))
        {
            throw new DomainNotFoundException("ERR_SERVER_NOT_FOUND", $"Server {dto.ServerUserId} introuvable.");
        }

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        // Open dine-in order envelope. Items + payment come later (future branches); the order
        // starts Open and stays Open until the bill is settled.
        var order = new Order
        {
            CashierUserId = actingUser.Id,
            BranchId = table.BranchId,
            Status = OrderStatus.Open,
            Channel = OrderChannel.DineIn,
            TableId = table.Id,
            OpenedAt = now,
            Subtotal = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            FinalAmount = 0,
        };
        await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct); // materialise order.Id

        var session = new TableSession
        {
            BranchId = table.BranchId,
            TableId = table.Id,
            ServerUserId = serverUserId,
            OrderId = order.Id,
            GuestCount = dto.GuestCount,
            OpenedAt = now,
            Notes = dto.Notes,
        };
        await _context.TableSessions.AddAsync(session, ct);

        table.Status = TableStatus.Occupied;
        table.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ProjectAsync(session.Id, ct);
    }

    public async Task<IReadOnlyList<TableSessionDto>> ListOpenForBranchAsync(int branchId, CancellationToken ct)
    {
        var sessions = await _context.TableSessions
            .Where(s => s.BranchId == branchId && s.ClosedAt == null)
            .Include(s => s.Table)
            .Include(s => s.ServerUser)
            .OrderBy(s => s.OpenedAt)
            .ToListAsync(ct);
        return _mapper.Map<List<TableSessionDto>>(sessions);
    }

    public async Task<TableSessionDto?> GetOpenForTableAsync(int tableId, CancellationToken ct)
    {
        var session = await _context.TableSessions
            .Where(s => s.TableId == tableId && s.ClosedAt == null)
            .Include(s => s.Table)
            .Include(s => s.ServerUser)
            .FirstOrDefaultAsync(ct);
        return session == null ? null : _mapper.Map<TableSessionDto>(session);
    }

    public async Task<TableSessionDto> TransferAsync(int sessionId, TransferTableDto dto, CancellationToken ct)
    {
        var session = await _context.TableSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.ClosedAt == null, ct)
            ?? throw new DomainNotFoundException("ERR_SESSION_NOT_FOUND",
                $"Open session {sessionId} introuvable.");

        if (session.TableId == dto.ToTableId)
            return await ProjectAsync(session.Id, ct); // no-op transfer

        var fromTable = await _context.Tables.FirstAsync(t => t.Id == session.TableId, ct);
        var toTable = await _context.Tables.FirstOrDefaultAsync(t => t.Id == dto.ToTableId && t.IsActive, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", $"Table {dto.ToTableId} introuvable.");

        if (toTable.BranchId != session.BranchId)
            throw new DomainException("ERR_TABLE_BRANCH_MISMATCH",
                "Cannot transfer a session to a table in a different branch.");

        var destinationOccupied = await _context.TableSessions
            .AnyAsync(s => s.TableId == toTable.Id && s.ClosedAt == null, ct);
        if (destinationOccupied || toTable.Status == TableStatus.Occupied)
            throw new DomainConflictException("ERR_TABLE_OCCUPIED",
                $"Table '{toTable.Name}' is already occupied.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        session.TableId = toTable.Id;

        // Keep the order's TableId in sync so receipts / KDS show the current table.
        if (session.OrderId.HasValue)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == session.OrderId.Value, ct);
            if (order != null) order.TableId = toTable.Id;
        }

        fromTable.Status = TableStatus.Dirty; // vacated table needs bussing
        fromTable.UpdatedAt = now;
        toTable.Status = TableStatus.Occupied;
        toTable.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ProjectAsync(session.Id, ct);
    }

    public async Task<TableSessionDto> MergeAsync(int sessionId, int intoSessionId, CancellationToken ct)
    {
        if (sessionId == intoSessionId)
            throw new DomainException("ERR_MERGE_SAME_SESSION", "Cannot merge a session into itself.");

        var source = await _context.TableSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.ClosedAt == null, ct)
            ?? throw new DomainNotFoundException("ERR_SESSION_NOT_FOUND", $"Open session {sessionId} introuvable.");
        var dest = await _context.TableSessions.FirstOrDefaultAsync(s => s.Id == intoSessionId && s.ClosedAt == null, ct)
            ?? throw new DomainNotFoundException("ERR_SESSION_NOT_FOUND", $"Open session {intoSessionId} introuvable.");

        if (source.BranchId != dest.BranchId)
            throw new DomainException("ERR_MERGE_BRANCH_MISMATCH", "Cannot merge sessions across branches.");
        if (source.OrderId is not int sourceOrderId || dest.OrderId is not int destOrderId)
            throw new DomainConflictException("ERR_MERGE_NO_ORDER", "Both sessions must have an order to merge.");

        // Merging once a bill is being settled would corrupt check math — block if either side
        // has checks.
        if (await _context.Checks.AnyAsync(c => c.OrderId == sourceOrderId || c.OrderId == destOrderId, ct))
            throw new DomainConflictException("ERR_MERGE_CHECKS_EXIST",
                "Cannot merge after a bill has been split. Re-split on the merged table instead.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        // Move the source order's items onto the destination order.
        var movingItems = await _context.OrderItems.Where(oi => oi.OrderId == sourceOrderId).ToListAsync(ct);
        foreach (var oi in movingItems) oi.OrderId = destOrderId;

        // Sum guest counts onto the destination session.
        dest.GuestCount += source.GuestCount;

        // Cancel the now-empty source order, close the source session, free its table.
        var sourceOrder = await _context.Orders.FirstAsync(o => o.Id == sourceOrderId, ct);
        _orderStates.AssertTransition(sourceOrder.Status, OrderStatus.Cancelled);
        sourceOrder.Status = OrderStatus.Cancelled;
        sourceOrder.ClosedAt = now;

        source.ClosedAt = now;
        var sourceTable = await _context.Tables.FirstOrDefaultAsync(t => t.Id == source.TableId, ct);
        if (sourceTable != null) { sourceTable.Status = TableStatus.Dirty; sourceTable.UpdatedAt = now; }

        await _context.SaveChangesAsync(ct);

        // Recompute the destination order's totals over the combined items.
        var destOrder = await _context.Orders.FirstAsync(o => o.Id == destOrderId, ct);
        await RecomputeTotalsAsync(destOrder, ct);

        await tx.CommitAsync(ct);
        return await ProjectAsync(dest.Id, ct);
    }

    public async Task CloseAsync(int sessionId, CancellationToken ct)
    {
        var session = await _context.TableSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.ClosedAt == null, ct)
            ?? throw new DomainNotFoundException("ERR_SESSION_NOT_FOUND",
                $"Open session {sessionId} introuvable.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        session.ClosedAt = now;

        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == session.TableId, ct);
        if (table != null)
        {
            // Vacated, awaiting bussing. ClearTableAsync flips Dirty → Free.
            table.Status = TableStatus.Dirty;
            table.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ClearTableAsync(int tableId, CancellationToken ct)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", $"Table {tableId} introuvable.");

        if (table.Status == TableStatus.Occupied)
            throw new DomainConflictException("ERR_TABLE_OCCUPIED",
                "Cannot clear an occupied table — close the open session first.");

        table.Status = TableStatus.Free;
        table.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    // ----- Order items --------------------------------------------------------------

    public async Task<DineInOrderDto> GetOrderAsync(int orderId, CancellationToken ct)
    {
        await LoadDineInOrderAsync(orderId, ct); // existence + channel guard
        return await ProjectOrderAsync(orderId, ct);
    }

    public async Task<DineInOrderDto> AddItemsAsync(int orderId, AddOrderItemsDto dto, CancellationToken ct)
    {
        var order = await LoadDineInOrderAsync(orderId, ct);
        if (order.Status != OrderStatus.Open)
            throw new DomainConflictException("ERR_ORDER_NOT_OPEN",
                "Items can only be added to an open order.");

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var categoryIds = products.Values.Select(p => p.CategoryId).Distinct().ToList();
        var stationByCategory = await _context.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.KitchenStationId, ct);

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        foreach (var line in dto.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new DomainException("ERR_PRODUCT_NOT_FOUND", $"Produit {line.ProductId} introuvable.");

            var modifierResult = await _modifierApp.PrepareAsync(
                line.ProductId,
                (IReadOnlyCollection<int>?)line.ModifierIds ?? Array.Empty<int>(),
                ct);

            var unitPrice = product.Price + modifierResult.TotalPriceDelta;
            stationByCategory.TryGetValue(product.CategoryId, out var stationId);

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * line.Quantity,
                TaxAmount = 0,
                Modifiers = "[]",
                KitchenStationId = stationId,
                CourseNumber = line.CourseNumber < 1 ? 1 : line.CourseNumber,
                Status = OrderItemStatus.Pending, // not fired until the server sends it
            };
            foreach (var snap in modifierResult.Snapshots) orderItem.AppliedModifiers.Add(snap);
            await _context.OrderItems.AddAsync(orderItem, ct);
        }

        await _context.SaveChangesAsync(ct);
        await RecomputeTotalsAsync(order, ct);
        await tx.CommitAsync(ct);

        return await ProjectOrderAsync(orderId, ct);
    }

    public async Task<DineInOrderDto> FireOrderAsync(int orderId, CancellationToken ct)
    {
        await LoadDineInOrderAsync(orderId, ct);

        var pending = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId && oi.Status == OrderItemStatus.Pending)
            .Select(oi => oi.Id)
            .ToListAsync(ct);

        // Delegate per-item to the KDS service so firing here behaves identically to firing
        // from a KDS screen (FiredAt stamp + station broadcast), no duplicated logic.
        foreach (var itemId in pending)
            await _kds.FireAsync(itemId, ct);

        return await ProjectOrderAsync(orderId, ct);
    }

    public async Task<DineInOrderDto> FireCourseAsync(int orderId, int courseNumber, CancellationToken ct)
    {
        await LoadDineInOrderAsync(orderId, ct);

        var pending = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId
                         && oi.CourseNumber == courseNumber
                         && oi.Status == OrderItemStatus.Pending)
            .Select(oi => oi.Id)
            .ToListAsync(ct);

        if (pending.Count == 0)
            throw new DomainConflictException("ERR_NO_PENDING_IN_COURSE",
                $"No pending items in course {courseNumber}.");

        foreach (var itemId in pending)
            await _kds.FireAsync(itemId, ct);

        return await ProjectOrderAsync(orderId, ct);
    }

    // ----- Helpers -----------------------------------------------------------------

    private async Task<Order> LoadDineInOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_NOT_FOUND", $"Order {orderId} introuvable.");
        if (order.Channel != OrderChannel.DineIn)
            throw new DomainException("ERR_NOT_DINE_IN", "This order is not a dine-in order.");
        return order;
    }

    private async Task RecomputeTotalsAsync(Order order, CancellationToken ct)
    {
        var subtotal = await _context.OrderItems
            .Where(oi => oi.OrderId == order.Id && oi.Status != OrderItemStatus.Voided)
            .SumAsync(oi => oi.LineTotal, ct);

        order.Subtotal = subtotal;
        order.FinalAmount = subtotal - order.DiscountAmount; // tax applied at payment (Phase B+)
        await _context.SaveChangesAsync(ct);
    }

    private async Task<DineInOrderDto> ProjectOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _context.Orders.FirstAsync(o => o.Id == orderId, ct);
        var items = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .Include(oi => oi.Product)
            .Include(oi => oi.AppliedModifiers)
            .OrderBy(oi => oi.Id)
            .ToListAsync(ct);

        return new DineInOrderDto
        {
            OrderId = order.Id,
            TableId = order.TableId,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            FinalAmount = order.FinalAmount,
            Items = items.Select(oi => new DineInOrderItemDto
            {
                OrderItemId = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.Product?.Name ?? "Unknown",
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                LineTotal = oi.LineTotal,
                Status = oi.Status.ToString(),
                KitchenStationId = oi.KitchenStationId,
                CourseNumber = oi.CourseNumber,
                Modifiers = oi.AppliedModifiers.Select(m => m.Name).ToList(),
            }).ToList(),
        };
    }

    private async Task<TableSessionDto> ProjectAsync(int sessionId, CancellationToken ct)
    {
        var session = await _context.TableSessions
            .Include(s => s.Table)
            .Include(s => s.ServerUser)
            .FirstAsync(s => s.Id == sessionId, ct);
        return _mapper.Map<TableSessionDto>(session);
    }
}
