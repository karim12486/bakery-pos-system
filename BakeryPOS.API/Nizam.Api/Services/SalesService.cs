using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

/// <summary>
/// All Sales business logic lives here. Controller is a thin shell that binds + delegates.
/// Pattern that the rest of the controllers will migrate to in PR-2b.
/// </summary>
public interface ISalesService
{
    Task<PagedResponse<SaleListDto>> ListAsync(PaginationParams pagination, DateTime? startDate, DateTime? endDate, CancellationToken ct);
    Task<SaleDetailDto?> GetAsync(int id, CancellationToken ct);
    Task<SaleCreatedDto> CreateAsync(SaleForCreateDto dto, string username, CancellationToken ct);
}

public sealed class SalesService : ISalesService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IValidator<SaleForCreateDto> _createValidator;
    private readonly IAuditService _audit;
    private readonly IModifierApplicationService _modifierApp;

    public SalesService(
        AppDbContext context,
        IMapper mapper,
        IValidator<SaleForCreateDto> createValidator,
        IAuditService audit,
        IModifierApplicationService modifierApp)
    {
        _context = context;
        _mapper = mapper;
        _createValidator = createValidator;
        _audit = audit;
        _modifierApp = modifierApp;
    }

    public async Task<PagedResponse<SaleListDto>> ListAsync(
        PaginationParams pagination, DateTime? startDate, DateTime? endDate, CancellationToken ct)
    {
        var query = _context.Sales.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(s => s.SaleDate >= startDate.Value);

        if (endDate.HasValue)
        {
            var effectiveEndDate = endDate.Value;
            if (effectiveEndDate.TimeOfDay == TimeSpan.Zero)
                effectiveEndDate = effectiveEndDate.Date.AddDays(1);
            query = query.Where(s => s.SaleDate < effectiveEndDate);
        }

        var totalRecords = await query.CountAsync(ct);

        var sales = await query
            .Include(s => s.User)
            .Include(s => s.Customer)
            .OrderByDescending(s => s.SaleDate)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        var dtos = _mapper.Map<IEnumerable<SaleListDto>>(sales);
        return new PagedResponse<SaleListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<SaleDetailDto?> GetAsync(int id, CancellationToken ct)
    {
        var sale = await _context.Sales
            .Include(s => s.User)
            .Include(s => s.Customer)
            .Include(s => s.SaleDetails).ThenInclude(sd => sd.Product)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return sale == null ? null : _mapper.Map<SaleDetailDto>(sale);
    }

    public async Task<SaleCreatedDto> CreateAsync(SaleForCreateDto dto, string username, CancellationToken ct)
    {
        // Shape validation. ValidationException bubbles up to ProblemDetailsMiddleware → 422.
        await _createValidator.ValidateAndThrowAsync(dto, ct);

        var cashier = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_CASHIER_NOT_FOUND", "Caissier introuvable.", StatusCodes.Status401Unauthorized);

        // A sale must happen inside an open shift. The Open POS Shift screen guarantees the
        // cashier has one before they reach the Cashier screen — but enforce server-side too
        // so the audit trail and Z-report are accurate even if a client bypasses the flow.
        var openShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == cashier.Id && s.ClosedAt == null, ct)
            ?? throw new DomainConflictException("ERR_NO_OPEN_SHIFT",
                "Vous devez ouvrir une session de caisse avant d'enregistrer une vente.");

        // Load all referenced products in one query.
        var productIds = dto.SaleDetails.Select(d => d.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Per-line preparation: validate modifier selections + compute price-per-unit including
        // modifier deltas + materialise snapshot rows. Done once, indexed by line position so the
        // create loop below stays a single pass over dto.SaleDetails.
        var linePrep = new List<LinePreparation>(dto.SaleDetails.Count);
        decimal totalAmount = 0;
        foreach (var item in dto.SaleDetails)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new DomainException("ERR_PRODUCT_NOT_FOUND", $"Produit avec l'ID {item.ProductId} introuvable.");

            var modifierResult = await _modifierApp.PrepareAsync(
                item.ProductId,
                (IReadOnlyCollection<int>?)item.ModifierIds ?? Array.Empty<int>(),
                ct);

            var unitPrice = product.Price + modifierResult.TotalPriceDelta;
            var lineTotal = unitPrice * item.Quantity;

            linePrep.Add(new LinePreparation(item, product, unitPrice, lineTotal, modifierResult.Snapshots));
            totalAmount += lineTotal;
        }

        // Customer + discount.
        decimal discountAmount = 0;
        Customer? customer = null;
        if (dto.CustomerId.HasValue)
        {
            customer = await _context.Customers.FindAsync(new object?[] { dto.CustomerId.Value }, ct)
                ?? throw new DomainException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");
            if (customer.DiscountPercentage > 0)
                discountAmount = totalAmount * (customer.DiscountPercentage / 100);
        }
        var finalAmount = totalAmount - discountAmount;

        // Payment math (preserved from original controller).
        var (cashPaid, cardPaid, totalPaidNow, changeGiven) = SplitPayment(dto, finalAmount);
        var amountOwed = finalAmount - totalPaidNow;

        // Owed money requires a customer to record the debt against.
        if (amountOwed > 0.001m && customer == null)
            throw new DomainException(
                "ERR_CUSTOMER_REQUIRED_FOR_DEBT",
                $"Paiement incomplet. Il reste {amountOwed:C} à payer. Un client doit être sélectionné pour enregistrer la dette.");

        // Wrap the multi-write in a transaction so partial failure = full rollback.
        using var tx = await _context.Database.BeginTransactionAsync(ct);

        if (amountOwed > 0.001m && customer != null)
            customer.CurrentBalance -= amountOwed;

        // ---- Order envelope (B-aware schema; Phase A creates orders closed-on-arrival) ----
        var now = DateTime.UtcNow;
        var newOrder = new Order
        {
            CashierUserId = cashier.Id,
            CustomerId = dto.CustomerId,
            ShiftId = openShift.Id,        // stamped from the cashier's open shift
            BranchId = openShift.BranchId, // shift's branch IS the order's branch
            Status = OrderStatus.Closed,
            Channel = OrderChannel.Takeaway,
            TableId = null,
            OpenedAt = now,
            ClosedAt = now,
            Subtotal = totalAmount,
            DiscountAmount = discountAmount,
            TaxAmount = 0, // Phase A doesn't price-out per-item tax yet
            FinalAmount = finalAmount
            // TenantId auto-stamped
        };
        await _context.Orders.AddAsync(newOrder, ct);

        // ---- Legacy Sale (the payment record) — points at the Order via OrderId ----
        var newSale = new Sale
        {
            UserId = cashier.Id,
            BranchId = openShift.BranchId,
            SaleDate = now,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            PaymentMethod = dto.PaymentMethod,
            AmountPaid = totalPaidNow,
            AmountOwed = amountOwed,
            ChangeGiven = changeGiven,
            CustomerId = dto.CustomerId,
            CashPaid = cashPaid,
            CardPaid = cardPaid,
            Order = newOrder
        };
        await _context.Sales.AddAsync(newSale, ct);

        // Stock decrement + ledger entries + OrderItem mirror.
        foreach (var prep in linePrep)
        {
            var item = prep.Item;
            var product = prep.Product;
            if (product.StockQuantity < item.Quantity)
                throw new DomainConflictException("ERR_INSUFFICIENT_STOCK", $"Stock insuffisant pour {product.Name}.");

            product.StockQuantity -= item.Quantity;

            // Legacy SaleDetail — kept during the transition. UnitPrice excludes modifier
            // deltas (legacy contract); the new OrderItem carries the modifier-inclusive price.
            await _context.SaleDetails.AddAsync(new SaleDetail
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Sale = newSale
            }, ct);

            // New OrderItem — canonical going forward. UnitPrice INCLUDES modifier deltas
            // so receipts and reports see the price the customer actually paid per unit.
            var orderItem = new OrderItem
            {
                Order = newOrder,
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = prep.UnitPrice,
                LineTotal = prep.LineTotal,
                TaxAmount = 0,
                Modifiers = "[]", // legacy column stays empty — snapshots live in AppliedModifiers
                Status = OrderItemStatus.Closed
            };
            foreach (var snap in prep.ModifierSnapshots) orderItem.AppliedModifiers.Add(snap);
            await _context.OrderItems.AddAsync(orderItem, ct);

            await _context.StockMovements.AddAsync(new StockMovement
            {
                ProductId = product.Id,
                UserId = cashier.Id,
                BranchId = openShift.BranchId,
                QuantityChange = -item.Quantity,
                Type = StockMovementType.Sale
            }, ct);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(AuditActions.SaleCreated, "Sale", newSale.Id,
            $"order={newOrder.Id};final={finalAmount};method={dto.PaymentMethod};owed={amountOwed}", ct: ct);

        return new SaleCreatedDto(newSale.Id, changeGiven);
    }

    /// <summary>Per-line working record built once during preparation and reused in the
    /// create loop. Carries the product, the modifier-inclusive unit price + line total,
    /// and the materialised snapshot rows to attach to the new <see cref="OrderItem"/>.</summary>
    private sealed record LinePreparation(
        SaleDetailForCreateDto Item,
        Product Product,
        decimal UnitPrice,
        decimal LineTotal,
        IReadOnlyList<OrderItemModifier> ModifierSnapshots);

    private static (decimal cashPaid, decimal cardPaid, decimal totalPaidNow, decimal changeGiven)
        SplitPayment(SaleForCreateDto dto, decimal finalAmount)
    {
        decimal cashPaid = 0, cardPaid = 0, totalPaidNow = 0, changeGiven = 0;

        switch (dto.PaymentMethod)
        {
            case PaymentType.Cash:
            {
                var tendered = dto.AmountPaid > 0 ? dto.AmountPaid : finalAmount;
                if (tendered >= finalAmount)
                {
                    totalPaidNow = finalAmount;
                    cashPaid = finalAmount;
                    changeGiven = tendered - finalAmount;
                }
                else
                {
                    totalPaidNow = tendered;
                    cashPaid = tendered;
                }
                break;
            }
            case PaymentType.Card:
            {
                var tendered = dto.AmountPaid > 0 ? dto.AmountPaid : finalAmount;
                if (tendered > finalAmount) tendered = finalAmount; // card can't give change
                totalPaidNow = tendered;
                cardPaid = tendered;
                break;
            }
            case PaymentType.Split:
            {
                cashPaid = dto.SplitCashAmount ?? 0;
                cardPaid = dto.SplitCardAmount ?? 0;
                totalPaidNow = cashPaid + cardPaid;
                if (totalPaidNow > finalAmount)
                {
                    changeGiven = totalPaidNow - finalAmount;
                    totalPaidNow = finalAmount;
                    cashPaid -= changeGiven;
                }
                break;
            }
            case PaymentType.Tab:
            {
                // "Tab" = pay later via the customer's running balance. AmountPaid is treated as
                // an optional cash deposit; the rest accumulates as debt on Customer.CurrentBalance.
                cashPaid = dto.AmountPaid;
                totalPaidNow = cashPaid;
                break;
            }
        }
        return (cashPaid, cardPaid, totalPaidNow, changeGiven);
    }
}

/// <summary>Returned to the client on a successful sale.</summary>
public sealed record SaleCreatedDto(int SaleId, decimal Change);
