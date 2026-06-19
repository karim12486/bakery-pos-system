using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
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

    /// <summary>Closes the session (guests gone / bill settled) and flips the table to Dirty.</summary>
    Task CloseAsync(int sessionId, CancellationToken ct);

    /// <summary>Marks a Dirty table clean and Free, ready for the next guests.</summary>
    Task ClearTableAsync(int tableId, CancellationToken ct);
}

public sealed class DineInService : IDineInService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public DineInService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
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

    // ----- Helpers -----------------------------------------------------------------

    private async Task<TableSessionDto> ProjectAsync(int sessionId, CancellationToken ct)
    {
        var session = await _context.TableSessions
            .Include(s => s.Table)
            .Include(s => s.ServerUser)
            .FirstAsync(s => s.Id == sessionId, ct);
        return _mapper.Map<TableSessionDto>(session);
    }
}
