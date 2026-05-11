using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IInventoryService
{
    Task<(string ProductName, int NewQuantity)> AddStockAsync(StockAdditionDto dto, string username, CancellationToken ct);
    Task<IReadOnlyList<StockMovementDto>> GetStockHistoryAsync(int productId, CancellationToken ct);
}

public sealed class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(string ProductName, int NewQuantity)> AddStockAsync(StockAdditionDto dto, string username, CancellationToken ct)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct)
            ?? throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", "Produit introuvable.");

        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        if (dto.QuantityToAdd <= 0)
            throw new DomainException("ERR_QUANTITY_NOT_POSITIVE", "La quantité à ajouter doit être supérieure à 0.");

        // Transaction so the Product update + StockMovement insert commit together.
        using var tx = await _context.Database.BeginTransactionAsync(ct);

        product.StockQuantity += dto.QuantityToAdd;
        await _context.StockMovements.AddAsync(new StockMovement
        {
            ProductId = product.Id,
            UserId = user.Id,
            QuantityChange = dto.QuantityToAdd,
            Type = StockMovementType.Addition
            // TenantId auto-stamped by AppDbContext.SaveChanges
        }, ct);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (product.Name, product.StockQuantity);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetStockHistoryAsync(int productId, CancellationToken ct)
    {
        var movements = await _context.StockMovements
            .Include(sm => sm.Product)
            .Include(sm => sm.User)
            .Where(sm => sm.ProductId == productId)
            .OrderByDescending(sm => sm.Timestamp)
            .ToListAsync(ct);

        return movements.Select(sm => new StockMovementDto
        {
            Id = sm.Id,
            Timestamp = sm.Timestamp,
            QuantityChange = sm.QuantityChange,
            Type = sm.Type.ToString(),
            ProductName = sm.Product.Name,
            UserName = sm.User.FullName
        }).ToList();
    }
}
