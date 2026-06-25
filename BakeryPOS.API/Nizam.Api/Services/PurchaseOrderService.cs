using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(PurchaseOrderStatus? status, CancellationToken ct);
    Task<PurchaseOrderDto> GetAsync(int id, CancellationToken ct);
    Task<PurchaseOrderDto> CreateAsync(PurchaseOrderCreateDto dto, string username, CancellationToken ct);
    Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct);

    /// <summary>Receives the order: increments product stock from the received quantities,
    /// writes a Purchase stock movement per line, updates product cost, marks Received.</summary>
    Task<PurchaseOrderDto> ReceiveAsync(int id, PurchaseOrderReceiveDto dto, string username, CancellationToken ct);

    Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _context;

    public PurchaseOrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(PurchaseOrderStatus? status, CancellationToken ct)
    {
        var query = _context.PurchaseOrders.AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .AsQueryable();
        if (status.HasValue) query = query.Where(po => po.Status == status.Value);
        var list = await query.OrderByDescending(po => po.OrderDate).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<PurchaseOrderDto> GetAsync(int id, CancellationToken ct)
        => ToDto(await FindWithLinesAsync(id, ct));

    public async Task<PurchaseOrderDto> CreateAsync(PurchaseOrderCreateDto dto, string username, CancellationToken ct)
    {
        var user = await ResolveUserAsync(username, ct);

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId, ct)
            ?? throw new DomainNotFoundException("ERR_SUPPLIER_NOT_FOUND", "Fournisseur introuvable.");
        if (!supplier.IsActive)
            throw new DomainConflictException("ERR_SUPPLIER_INACTIVE", "Ce fournisseur est désactivé.");

        if (dto.BranchId is int branchId)
            await EnsureBranchExistsAsync(branchId, ct);

        await EnsureProductsExistAsync(dto.Items.Select(i => i.ProductId), ct);
        if (dto.Items.Select(i => i.ProductId).Distinct().Count() != dto.Items.Count)
            throw new DomainException("ERR_PO_DUPLICATE_PRODUCT", "Un produit ne peut apparaître qu'une seule fois.");

        var po = new PurchaseOrder
        {
            SupplierId = supplier.Id,
            BranchId = dto.BranchId,
            Status = PurchaseOrderStatus.Draft,
            Reference = dto.Reference?.Trim(),
            Notes = dto.Notes?.Trim(),
            OrderDate = DateTime.UtcNow,
            ExpectedDate = dto.ExpectedDate,
            CreatedByUserId = user.Id,
            Items = dto.Items.Select(i => new PurchaseOrderItem
            {
                ProductId = i.ProductId,
                QuantityOrdered = i.QuantityOrdered,
                QuantityReceived = 0,
                UnitCost = i.UnitCost
            }).ToList()
        };
        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(ct);
        return ToDto(await FindWithLinesAsync(po.Id, ct));
    }

    public async Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct)
    {
        var po = await FindWithLinesAsync(id, ct);
        if (po.Status != PurchaseOrderStatus.Draft)
            throw new DomainConflictException("ERR_PO_NOT_DRAFT", "Seules les commandes en brouillon peuvent être soumises.");
        po.Status = PurchaseOrderStatus.Submitted;
        po.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(po);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(int id, PurchaseOrderReceiveDto dto, string username, CancellationToken ct)
    {
        var user = await ResolveUserAsync(username, ct);
        var po = await FindWithLinesAsync(id, ct);

        if (po.Status is not (PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Draft))
            throw new DomainConflictException("ERR_PO_NOT_RECEIVABLE",
                "Seules les commandes en brouillon ou soumises peuvent être réceptionnées.");

        var receivedByProduct = dto.Lines.ToDictionary(l => l.ProductId, l => l.QuantityReceived);

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        foreach (var line in po.Items)
        {
            // Default to ordered quantity when the caller didn't specify a received amount.
            var qty = receivedByProduct.TryGetValue(line.ProductId, out var v) ? v : line.QuantityOrdered;
            line.QuantityReceived = qty;
            if (qty <= 0) continue;

            var product = await _context.Products.FirstAsync(p => p.Id == line.ProductId, ct);
            product.StockQuantity += qty;
            // Keep the product's cost basis in step with the latest purchase price.
            if (line.UnitCost > 0) product.CostPrice = line.UnitCost;

            _context.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                UserId = user.Id,
                BranchId = po.BranchId,
                QuantityChange = qty,
                Type = StockMovementType.Purchase
            });
        }

        po.Status = PurchaseOrderStatus.Received;
        po.ReceivedDate = DateTime.UtcNow;
        po.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(po);
    }

    public async Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct)
    {
        var po = await FindWithLinesAsync(id, ct);
        if (po.Status == PurchaseOrderStatus.Received)
            throw new DomainConflictException("ERR_PO_ALREADY_RECEIVED", "Une commande réceptionnée ne peut être annulée.");
        po.Status = PurchaseOrderStatus.Cancelled;
        po.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(po);
    }

    // ---- helpers ----

    private async Task<User> ResolveUserAsync(string username, CancellationToken ct)
        => await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
           ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

    private async Task EnsureBranchExistsAsync(int branchId, CancellationToken ct)
    {
        if (!await _context.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", "Branche introuvable.");
    }

    private async Task EnsureProductsExistAsync(IEnumerable<int> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var found = await _context.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (found.Count != ids.Count)
            throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", "Un ou plusieurs produits sont introuvables.");
    }

    private async Task<PurchaseOrder> FindWithLinesAsync(int id, CancellationToken ct)
        => await _context.PurchaseOrders
               .Include(po => po.Supplier)
               .Include(po => po.Items).ThenInclude(i => i.Product)
               .FirstOrDefaultAsync(po => po.Id == id, ct)
           ?? throw new DomainNotFoundException("ERR_PO_NOT_FOUND", "Bon de commande introuvable.");

    private static PurchaseOrderDto ToDto(PurchaseOrder po) => new()
    {
        Id = po.Id,
        SupplierId = po.SupplierId,
        SupplierName = po.Supplier?.Name ?? string.Empty,
        BranchId = po.BranchId,
        Status = po.Status.ToString(),
        Reference = po.Reference,
        Notes = po.Notes,
        OrderDate = po.OrderDate,
        ExpectedDate = po.ExpectedDate,
        ReceivedDate = po.ReceivedDate,
        Total = po.Items.Sum(i => i.UnitCost * i.QuantityOrdered),
        Items = po.Items.Select(i => new PurchaseOrderLineDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            QuantityOrdered = i.QuantityOrdered,
            QuantityReceived = i.QuantityReceived,
            UnitCost = i.UnitCost,
            LineTotal = i.UnitCost * i.QuantityOrdered
        }).ToList()
    };
}
