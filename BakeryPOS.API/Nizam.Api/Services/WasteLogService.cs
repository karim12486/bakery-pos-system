using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IWasteLogService
{
    Task<IReadOnlyList<WasteLogEntryDto>> ListAsync(int? productId, CancellationToken ct);

    /// <summary>Records a write-off: decrements product stock, writes a Waste stock movement,
    /// and stores the entry with its estimated cost (qty × current cost price).</summary>
    Task<WasteLogEntryDto> RecordAsync(WasteLogCreateDto dto, string username, CancellationToken ct);
}

public sealed class WasteLogService : IWasteLogService
{
    private readonly AppDbContext _context;

    public WasteLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WasteLogEntryDto>> ListAsync(int? productId, CancellationToken ct)
    {
        var query = _context.WasteLogEntries.AsNoTracking()
            .Include(w => w.Product)
            .AsQueryable();
        if (productId.HasValue) query = query.Where(w => w.ProductId == productId.Value);
        var list = await query.OrderByDescending(w => w.CreatedAt).Take(200).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<WasteLogEntryDto> RecordAsync(WasteLogCreateDto dto, string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        if (dto.Quantity <= 0)
            throw new DomainException("ERR_QUANTITY_NOT_POSITIVE", "La quantité doit être supérieure à 0.");

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct)
            ?? throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", "Produit introuvable.");

        if (dto.BranchId is int branchId && !await _context.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", "Branche introuvable.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        product.StockQuantity -= dto.Quantity;

        var entry = new WasteLogEntry
        {
            ProductId = product.Id,
            BranchId = dto.BranchId,
            Quantity = dto.Quantity,
            Reason = dto.Reason,
            EstimatedCost = product.CostPrice * dto.Quantity,
            Notes = dto.Notes?.Trim(),
            RecordedByUserId = user.Id
        };
        _context.WasteLogEntries.Add(entry);

        _context.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            UserId = user.Id,
            BranchId = dto.BranchId,
            QuantityChange = -dto.Quantity,
            Type = StockMovementType.Waste
        });

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        entry.Product = product;
        return ToDto(entry);
    }

    private static WasteLogEntryDto ToDto(WasteLogEntry w) => new()
    {
        Id = w.Id,
        ProductId = w.ProductId,
        ProductName = w.Product?.Name ?? string.Empty,
        BranchId = w.BranchId,
        Quantity = w.Quantity,
        Reason = w.Reason.ToString(),
        EstimatedCost = w.EstimatedCost,
        Notes = w.Notes,
        CreatedAt = w.CreatedAt
    };
}
