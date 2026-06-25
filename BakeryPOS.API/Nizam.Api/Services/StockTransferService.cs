using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IStockTransferService
{
    Task<IReadOnlyList<StockTransferDto>> ListAsync(StockTransferStatus? status, CancellationToken ct);
    Task<StockTransferDto> GetAsync(int id, CancellationToken ct);
    Task<StockTransferDto> CreateAsync(StockTransferCreateDto dto, string username, CancellationToken ct);

    /// <summary>Dispatches the transfer: writes a TransferOut movement per line (tagged to the
    /// source branch) and marks Sent.</summary>
    Task<StockTransferDto> SendAsync(int id, string username, CancellationToken ct);

    /// <summary>Confirms receipt: writes a TransferIn movement per line (tagged to the
    /// destination branch) and marks Received.</summary>
    Task<StockTransferDto> ReceiveAsync(int id, string username, CancellationToken ct);

    Task<StockTransferDto> CancelAsync(int id, CancellationToken ct);
}

public sealed class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _context;

    public StockTransferService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StockTransferDto>> ListAsync(StockTransferStatus? status, CancellationToken ct)
    {
        var query = _context.StockTransfers.AsNoTracking()
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .AsQueryable();
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<StockTransferDto> GetAsync(int id, CancellationToken ct)
        => ToDto(await FindWithLinesAsync(id, ct));

    public async Task<StockTransferDto> CreateAsync(StockTransferCreateDto dto, string username, CancellationToken ct)
    {
        var user = await ResolveUserAsync(username, ct);

        if (dto.FromBranchId == dto.ToBranchId)
            throw new DomainException("ERR_TRANSFER_SAME_BRANCH", "La source et la destination doivent être différentes.");
        await EnsureBranchExistsAsync(dto.FromBranchId, ct);
        await EnsureBranchExistsAsync(dto.ToBranchId, ct);

        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        if (productIds.Distinct().Count() != productIds.Count)
            throw new DomainException("ERR_TRANSFER_DUPLICATE_PRODUCT", "Un produit ne peut apparaître qu'une seule fois.");
        await EnsureProductsExistAsync(productIds, ct);

        var transfer = new StockTransfer
        {
            FromBranchId = dto.FromBranchId,
            ToBranchId = dto.ToBranchId,
            Status = StockTransferStatus.Draft,
            Reference = dto.Reference?.Trim(),
            Notes = dto.Notes?.Trim(),
            CreatedByUserId = user.Id,
            Items = dto.Items.Select(i => new StockTransferItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };
        _context.StockTransfers.Add(transfer);
        await _context.SaveChangesAsync(ct);
        return ToDto(await FindWithLinesAsync(transfer.Id, ct));
    }

    public async Task<StockTransferDto> SendAsync(int id, string username, CancellationToken ct)
    {
        var user = await ResolveUserAsync(username, ct);
        var transfer = await FindWithLinesAsync(id, ct);
        if (transfer.Status != StockTransferStatus.Draft)
            throw new DomainConflictException("ERR_TRANSFER_NOT_DRAFT", "Seuls les transferts en brouillon peuvent être envoyés.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        foreach (var line in transfer.Items)
        {
            _context.StockMovements.Add(new StockMovement
            {
                ProductId = line.ProductId,
                UserId = user.Id,
                BranchId = transfer.FromBranchId,
                QuantityChange = -line.Quantity,
                Type = StockMovementType.TransferOut
            });
        }
        transfer.Status = StockTransferStatus.Sent;
        transfer.SentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(transfer);
    }

    public async Task<StockTransferDto> ReceiveAsync(int id, string username, CancellationToken ct)
    {
        var user = await ResolveUserAsync(username, ct);
        var transfer = await FindWithLinesAsync(id, ct);
        if (transfer.Status != StockTransferStatus.Sent)
            throw new DomainConflictException("ERR_TRANSFER_NOT_SENT", "Seuls les transferts envoyés peuvent être réceptionnés.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        foreach (var line in transfer.Items)
        {
            _context.StockMovements.Add(new StockMovement
            {
                ProductId = line.ProductId,
                UserId = user.Id,
                BranchId = transfer.ToBranchId,
                QuantityChange = line.Quantity,
                Type = StockMovementType.TransferIn
            });
        }
        transfer.Status = StockTransferStatus.Received;
        transfer.ReceivedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(transfer);
    }

    public async Task<StockTransferDto> CancelAsync(int id, CancellationToken ct)
    {
        var transfer = await FindWithLinesAsync(id, ct);
        if (transfer.Status == StockTransferStatus.Received)
            throw new DomainConflictException("ERR_TRANSFER_ALREADY_RECEIVED", "Un transfert réceptionné ne peut être annulé.");
        transfer.Status = StockTransferStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        return ToDto(transfer);
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

    private async Task<StockTransfer> FindWithLinesAsync(int id, CancellationToken ct)
        => await _context.StockTransfers
               .Include(t => t.Items).ThenInclude(i => i.Product)
               .FirstOrDefaultAsync(t => t.Id == id, ct)
           ?? throw new DomainNotFoundException("ERR_TRANSFER_NOT_FOUND", "Transfert introuvable.");

    private static StockTransferDto ToDto(StockTransfer t) => new()
    {
        Id = t.Id,
        FromBranchId = t.FromBranchId,
        ToBranchId = t.ToBranchId,
        Status = t.Status.ToString(),
        Reference = t.Reference,
        Notes = t.Notes,
        CreatedAt = t.CreatedAt,
        SentAt = t.SentAt,
        ReceivedAt = t.ReceivedAt,
        Items = t.Items.Select(i => new StockTransferLineDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Quantity = i.Quantity
        }).ToList()
    };
}
