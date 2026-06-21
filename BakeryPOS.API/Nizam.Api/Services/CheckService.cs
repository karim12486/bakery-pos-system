using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services.Orders;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ICheckService
{
    Task<IReadOnlyList<CheckDto>> GetForOrderAsync(int orderId, CancellationToken ct);

    /// <summary>(Re)splits an order into checks per the chosen mode. Replaces existing UNPAID
    /// checks; blocked if any check is already paid.</summary>
    Task<IReadOnlyList<CheckDto>> CreateChecksAsync(int orderId, CreateChecksDto dto, CancellationToken ct);

    /// <summary>Toggle the automatic 12% service charge on a single open check + recompute it.</summary>
    Task<CheckDto> SetServiceChargeAsync(int checkId, bool optedOut, CancellationToken ct);

    /// <summary>Settle a check. When the order's last open check is paid, the order closes.</summary>
    Task<CheckDto> PayAsync(int checkId, PaymentType method, CancellationToken ct);
}

public sealed class CheckService : ICheckService
{
    /// <summary>Egyptian restaurant norm. Per-tenant Settings override is a future enhancement.</summary>
    private const decimal ServiceChargeRate = 0.12m;

    private readonly AppDbContext _context;
    private readonly IOrderStateMachine _orderStates;

    public CheckService(AppDbContext context, IOrderStateMachine orderStates)
    {
        _context = context;
        _orderStates = orderStates;
    }

    public async Task<IReadOnlyList<CheckDto>> GetForOrderAsync(int orderId, CancellationToken ct)
    {
        var checks = await _context.Checks
            .Where(c => c.OrderId == orderId)
            .Include(c => c.Items)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        return checks.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CheckDto>> CreateChecksAsync(int orderId, CreateChecksDto dto, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new DomainNotFoundException("ERR_ORDER_NOT_FOUND", $"Order {orderId} introuvable.");

        var existing = await _context.Checks.Where(c => c.OrderId == orderId).ToListAsync(ct);
        if (existing.Any(c => c.Status == CheckStatus.Paid))
            throw new DomainConflictException("ERR_CHECK_ALREADY_PAID",
                "Cannot re-split: one or more checks are already paid.");

        var items = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId && oi.Status != OrderItemStatus.Voided)
            .ToListAsync(ct);
        if (items.Count == 0)
            throw new DomainConflictException("ERR_ORDER_EMPTY", "Order has no items to split.");

        var taxRate = await ResolveTaxRateAsync(order, ct);

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        // Replace any existing (unpaid) checks.
        if (existing.Count > 0)
        {
            _context.Checks.RemoveRange(existing);
            await _context.SaveChangesAsync(ct);
        }

        var checks = dto.Mode switch
        {
            SplitMode.Single => BuildSingle(order, items, taxRate),
            SplitMode.ByItem => BuildByItem(order, items, dto, taxRate),
            SplitMode.Equal => BuildEqual(order, items, dto, taxRate),
            SplitMode.Percentage => BuildPercentage(order, items, dto, taxRate),
            _ => throw new DomainException("ERR_INVALID_SPLIT_MODE", "Unknown split mode."),
        };

        _context.Checks.AddRange(checks);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetForOrderAsync(orderId, ct);
    }

    public async Task<CheckDto> SetServiceChargeAsync(int checkId, bool optedOut, CancellationToken ct)
    {
        var check = await _context.Checks.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == checkId, ct)
            ?? throw new DomainNotFoundException("ERR_CHECK_NOT_FOUND", $"Check {checkId} introuvable.");
        if (check.Status != CheckStatus.Open)
            throw new DomainConflictException("ERR_CHECK_NOT_OPEN", "Only an open check can be modified.");

        var order = await _context.Orders.FirstAsync(o => o.Id == check.OrderId, ct);
        var taxRate = await ResolveTaxRateAsync(order, ct);

        check.ServiceChargeOptedOut = optedOut;
        ApplyMoney(check, check.Subtotal, taxRate);
        await _context.SaveChangesAsync(ct);
        return ToDto(check);
    }

    public async Task<CheckDto> PayAsync(int checkId, PaymentType method, CancellationToken ct)
    {
        var check = await _context.Checks.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == checkId, ct)
            ?? throw new DomainNotFoundException("ERR_CHECK_NOT_FOUND", $"Check {checkId} introuvable.");
        if (check.Status == CheckStatus.Paid)
            throw new DomainConflictException("ERR_CHECK_ALREADY_PAID", "This check is already paid.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        check.Status = CheckStatus.Paid;
        check.PaymentMethod = method;
        check.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Order closes once no open checks remain.
        var anyOpen = await _context.Checks
            .AnyAsync(c => c.OrderId == check.OrderId && c.Status == CheckStatus.Open, ct);
        if (!anyOpen)
        {
            var order = await _context.Orders.FirstAsync(o => o.Id == check.OrderId, ct);
            if (order.Status != OrderStatus.Closed)
            {
                _orderStates.AssertTransition(order.Status, OrderStatus.Closed);
                order.Status = OrderStatus.Closed;
                order.ClosedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
        }

        await tx.CommitAsync(ct);
        return ToDto(check);
    }

    // ----- Builders -----------------------------------------------------------------

    private List<Check> BuildSingle(Order order, List<OrderItem> items, decimal taxRate)
    {
        var check = NewCheck(order, "Check 1", items.Sum(i => i.LineTotal), taxRate);
        foreach (var oi in items) check.Items.Add(new CheckItem { OrderItemId = oi.Id });
        return new List<Check> { check };
    }

    private List<Check> BuildByItem(Order order, List<OrderItem> items, CreateChecksDto dto, decimal taxRate)
    {
        var assignments = dto.Assignments
            ?? throw new DomainException("ERR_ASSIGNMENTS_REQUIRED", "ByItem split requires assignments.");
        if (assignments.Count == 0)
            throw new DomainException("ERR_ASSIGNMENTS_REQUIRED", "At least one check assignment is required.");

        var itemById = items.ToDictionary(i => i.Id);
        var seen = new HashSet<int>();

        var checks = new List<Check>();
        for (var idx = 0; idx < assignments.Count; idx++)
        {
            var a = assignments[idx];
            var label = string.IsNullOrWhiteSpace(a.Label) ? $"Check {idx + 1}" : a.Label!;
            decimal subtotal = 0;
            var check = NewCheck(order, label, 0, taxRate);

            foreach (var itemId in a.OrderItemIds)
            {
                if (!itemById.TryGetValue(itemId, out var oi))
                    throw new DomainException("ERR_ITEM_NOT_ON_ORDER",
                        $"Order item {itemId} is not on this order (or is voided).");
                if (!seen.Add(itemId))
                    throw new DomainException("ERR_ITEM_DUPLICATED",
                        $"Order item {itemId} is assigned to more than one check.");
                subtotal += oi.LineTotal;
                check.Items.Add(new CheckItem { OrderItemId = itemId });
            }

            ApplyMoney(check, subtotal, taxRate);
            checks.Add(check);
        }

        // Every non-voided item must be assigned to exactly one check.
        var unassigned = items.Select(i => i.Id).Where(id => !seen.Contains(id)).ToList();
        if (unassigned.Count > 0)
            throw new DomainException("ERR_ITEMS_UNASSIGNED",
                $"These items are not on any check: {string.Join(", ", unassigned)}.");

        return checks;
    }

    private List<Check> BuildEqual(Order order, List<OrderItem> items, CreateChecksDto dto, decimal taxRate)
    {
        var n = dto.SplitCount ?? throw new DomainException("ERR_SPLIT_COUNT_REQUIRED", "Equal split requires SplitCount.");
        if (n < 2) throw new DomainException("ERR_SPLIT_COUNT_INVALID", "Split count must be at least 2.");

        var orderSubtotal = items.Sum(i => i.LineTotal);
        var shares = DivideEvenly(orderSubtotal, n);

        var checks = new List<Check>();
        for (var i = 0; i < n; i++)
            checks.Add(NewCheck(order, $"Check {i + 1}", shares[i], taxRate));
        return checks;
    }

    private List<Check> BuildPercentage(Order order, List<OrderItem> items, CreateChecksDto dto, decimal taxRate)
    {
        var pcts = dto.Percentages
            ?? throw new DomainException("ERR_PERCENTAGES_REQUIRED", "Percentage split requires Percentages.");
        if (pcts.Count < 2) throw new DomainException("ERR_PERCENTAGES_INVALID", "Provide at least two percentages.");
        if (Math.Abs(pcts.Sum() - 100m) > 0.01m)
            throw new DomainException("ERR_PERCENTAGES_SUM", "Percentages must sum to 100.");

        var orderSubtotal = items.Sum(i => i.LineTotal);
        var checks = new List<Check>();
        decimal allocated = 0;
        for (var i = 0; i < pcts.Count; i++)
        {
            // Last check absorbs the rounding remainder so the parts sum exactly to the subtotal.
            var share = i == pcts.Count - 1
                ? orderSubtotal - allocated
                : Math.Round(orderSubtotal * pcts[i] / 100m, 2, MidpointRounding.AwayFromZero);
            allocated += share;
            checks.Add(NewCheck(order, $"Check {i + 1}", share, taxRate));
        }
        return checks;
    }

    // ----- Helpers ------------------------------------------------------------------

    private Check NewCheck(Order order, string label, decimal subtotal, decimal taxRate)
    {
        var check = new Check
        {
            OrderId = order.Id,
            Label = label,
            Status = CheckStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };
        ApplyMoney(check, subtotal, taxRate);
        return check;
    }

    /// <summary>Recomputes a check's money: service charge (12% unless opted out) + tax on
    /// subtotal, then the total. Single source of truth for check math.</summary>
    private static void ApplyMoney(Check check, decimal subtotal, decimal taxRate)
    {
        check.Subtotal = subtotal;
        check.ServiceChargeAmount = check.ServiceChargeOptedOut
            ? 0m
            : Math.Round(subtotal * ServiceChargeRate, 2, MidpointRounding.AwayFromZero);
        check.TaxAmount = Math.Round(subtotal * taxRate, 2, MidpointRounding.AwayFromZero);
        check.Total = check.Subtotal + check.ServiceChargeAmount + check.TaxAmount;
    }

    /// <summary>Splits an amount into <paramref name="n"/> parts that sum exactly to it, with
    /// the rounding remainder on the first part.</summary>
    private static decimal[] DivideEvenly(decimal amount, int n)
    {
        var each = Math.Round(amount / n, 2, MidpointRounding.AwayFromZero);
        var parts = new decimal[n];
        for (var i = 0; i < n; i++) parts[i] = each;
        var drift = amount - each * n;
        parts[0] += drift; // remainder to the first check
        return parts;
    }

    private async Task<decimal> ResolveTaxRateAsync(Order order, CancellationToken ct)
    {
        if (order.BranchId is not int branchId) return 0m;
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct);
        return branch?.TaxRate ?? 0m;
    }

    private static CheckDto ToDto(Check c) => new()
    {
        Id = c.Id,
        OrderId = c.OrderId,
        Label = c.Label,
        Subtotal = c.Subtotal,
        ServiceChargeAmount = c.ServiceChargeAmount,
        TaxAmount = c.TaxAmount,
        Total = c.Total,
        ServiceChargeOptedOut = c.ServiceChargeOptedOut,
        Status = c.Status.ToString(),
        PaymentMethod = c.PaymentMethod?.ToString(),
        PaidAt = c.PaidAt,
        OrderItemIds = c.Items.Select(i => i.OrderItemId).ToList(),
    };
}
