using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IShiftService
{
    Task<ShiftDto> OpenAsync(OpenShiftDto dto, string username, CancellationToken ct);
    Task<ZReportDto> CloseAsync(int shiftId, CloseShiftDto dto, string username, CancellationToken ct);
    Task<ShiftDto?> GetCurrentAsync(string username, CancellationToken ct);
    Task<ShiftDto?> GetAsync(int id, CancellationToken ct);
    Task<ZReportDto?> GetZReportAsync(int shiftId, CancellationToken ct);
}

public sealed class ShiftService : IShiftService
{
    private readonly AppDbContext _context;

    public ShiftService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftDto> OpenAsync(OpenShiftDto dto, string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        // Verify branch exists in this tenant — filter scopes the lookup automatically.
        var branch = await _context.Branches.SingleOrDefaultAsync(b => b.Id == dto.BranchId, ct)
            ?? throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", "Branche introuvable.");

        if (!branch.IsActive)
            throw new DomainConflictException("ERR_BRANCH_INACTIVE", "Cette branche est désactivée.");

        // One open shift per (user, branch) at a time.
        var existing = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == user.Id && s.ClosedAt == null, ct);
        if (existing != null)
        {
            throw new DomainConflictException("ERR_SHIFT_ALREADY_OPEN",
                "Vous avez déjà une session ouverte. Veuillez la fermer avant d'en ouvrir une nouvelle.");
        }

        var shift = new Shift
        {
            BranchId = dto.BranchId,
            UserId = user.Id,
            OpeningFloat = dto.OpeningFloat,
            OpenedAt = DateTime.UtcNow
            // TenantId auto-stamped by AppDbContext
        };

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync(ct);

        return Map(shift, user.FullName);
    }

    public async Task<ZReportDto> CloseAsync(int shiftId, CloseShiftDto dto, string username, CancellationToken ct)
    {
        var shift = await _context.Shifts
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw new DomainNotFoundException("ERR_SHIFT_NOT_FOUND", "Session introuvable.");

        if (shift.User?.Username != username)
            throw new DomainException("ERR_SHIFT_NOT_OWNED",
                "Vous ne pouvez fermer que votre propre session.", StatusCodes.Status403Forbidden);

        if (shift.ClosedAt != null)
            throw new DomainConflictException("ERR_SHIFT_ALREADY_CLOSED",
                "Cette session a déjà été fermée.");

        // Compute totals from this shift's orders. Order.ShiftId not yet populated (Phase A's
        // SalesService.CreateAsync doesn't know about shifts yet — wired in a follow-up that
        // links SalesController to "open shift required"). For now we compute over the shift's
        // time window for the cashier — temporary but accurate while the wiring catches up.
        var orders = await _context.Orders
            .Where(o => o.CashierUserId == shift.UserId
                     && o.OpenedAt >= shift.OpenedAt
                     && o.OpenedAt <= DateTime.UtcNow
                     && o.Status == OrderStatus.Closed)
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();

        // Pull the matching Sales (which carry the cash/card breakdown).
        var sales = await _context.Sales
            .Where(s => s.OrderId != null && orderIds.Contains(s.OrderId!.Value))
            .ToListAsync(ct);

        var cashTaken = sales.Sum(s => s.CashPaid);
        var cardTaken = sales.Sum(s => s.CardPaid);
        var tabExtended = sales.Sum(s => s.AmountOwed);
        var grossSales = orders.Sum(o => o.Subtotal);
        var discounts = orders.Sum(o => o.DiscountAmount);
        var netSales = orders.Sum(o => o.FinalAmount);

        var expectedCash = shift.OpeningFloat + cashTaken;
        var variance = dto.ClosingCount - expectedCash;

        var now = DateTime.UtcNow;
        shift.ClosedAt = now;
        shift.ClosingCount = dto.ClosingCount;
        shift.ExpectedCash = expectedCash;
        shift.Variance = variance;
        shift.VarianceNotes = dto.VarianceNotes;

        await _context.SaveChangesAsync(ct);

        return new ZReportDto
        {
            ShiftId = shift.Id,
            OpenedAt = shift.OpenedAt,
            ClosedAt = now,
            CashierName = shift.User?.FullName ?? "Unknown",
            OpeningFloat = shift.OpeningFloat,
            ClosingCount = dto.ClosingCount,
            ExpectedCash = expectedCash,
            Variance = variance,
            OrderCount = orders.Count,
            GrossSales = grossSales,
            Discounts = discounts,
            NetSales = netSales,
            CashTaken = cashTaken,
            CardTaken = cardTaken,
            TabExtended = tabExtended
        };
    }

    public async Task<ShiftDto?> GetCurrentAsync(string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user == null) return null;

        var shift = await _context.Shifts
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == user.Id && s.ClosedAt == null, ct);

        return shift == null ? null : Map(shift, shift.User?.FullName ?? user.FullName);
    }

    public async Task<ShiftDto?> GetAsync(int id, CancellationToken ct)
    {
        var shift = await _context.Shifts.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id, ct);
        return shift == null ? null : Map(shift, shift.User?.FullName ?? "Unknown");
    }

    public async Task<ZReportDto?> GetZReportAsync(int shiftId, CancellationToken ct)
    {
        var shift = await _context.Shifts.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift == null || shift.ClosedAt == null) return null;

        var orders = await _context.Orders
            .Where(o => o.CashierUserId == shift.UserId
                     && o.OpenedAt >= shift.OpenedAt
                     && o.OpenedAt <= shift.ClosedAt
                     && o.Status == OrderStatus.Closed)
            .ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var sales = await _context.Sales
            .Where(s => s.OrderId != null && orderIds.Contains(s.OrderId!.Value))
            .ToListAsync(ct);

        return new ZReportDto
        {
            ShiftId = shift.Id,
            OpenedAt = shift.OpenedAt,
            ClosedAt = shift.ClosedAt!.Value,
            CashierName = shift.User?.FullName ?? "Unknown",
            OpeningFloat = shift.OpeningFloat,
            ClosingCount = shift.ClosingCount ?? 0,
            ExpectedCash = shift.ExpectedCash ?? 0,
            Variance = shift.Variance ?? 0,
            OrderCount = orders.Count,
            GrossSales = orders.Sum(o => o.Subtotal),
            Discounts = orders.Sum(o => o.DiscountAmount),
            NetSales = orders.Sum(o => o.FinalAmount),
            CashTaken = sales.Sum(s => s.CashPaid),
            CardTaken = sales.Sum(s => s.CardPaid),
            TabExtended = sales.Sum(s => s.AmountOwed)
        };
    }

    private static ShiftDto Map(Shift s, string userFullName) => new()
    {
        Id = s.Id,
        BranchId = s.BranchId,
        UserId = s.UserId,
        UserFullName = userFullName,
        OpeningFloat = s.OpeningFloat,
        OpenedAt = s.OpenedAt,
        ClosedAt = s.ClosedAt,
        ClosingCount = s.ClosingCount,
        ExpectedCash = s.ExpectedCash,
        Variance = s.Variance,
        VarianceNotes = s.VarianceNotes
    };
}
