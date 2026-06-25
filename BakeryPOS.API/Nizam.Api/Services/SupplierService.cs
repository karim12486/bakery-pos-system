using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<SupplierDto> GetAsync(int id, CancellationToken ct);
    Task<SupplierDto> CreateAsync(SupplierUpsertDto dto, CancellationToken ct);
    Task<SupplierDto> UpdateAsync(int id, SupplierUpsertDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);
}

public sealed class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;

    public SupplierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Suppliers.AsNoTracking();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Name).Select(s => ToDto(s)).ToListAsync(ct);
    }

    public async Task<SupplierDto> GetAsync(int id, CancellationToken ct)
        => ToDto(await FindAsync(id, ct));

    public async Task<SupplierDto> CreateAsync(SupplierUpsertDto dto, CancellationToken ct)
    {
        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            ContactName = dto.ContactName?.Trim(),
            Phone = dto.Phone?.Trim(),
            Email = dto.Email?.Trim(),
            Notes = dto.Notes?.Trim(),
            IsActive = dto.IsActive
        };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(int id, SupplierUpsertDto dto, CancellationToken ct)
    {
        var supplier = await FindAsync(id, ct);
        supplier.Name = dto.Name.Trim();
        supplier.ContactName = dto.ContactName?.Trim();
        supplier.Phone = dto.Phone?.Trim();
        supplier.Email = dto.Email?.Trim();
        supplier.Notes = dto.Notes?.Trim();
        supplier.IsActive = dto.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var supplier = await FindAsync(id, ct);
        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private async Task<Supplier> FindAsync(int id, CancellationToken ct)
        => await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
           ?? throw new DomainNotFoundException("ERR_SUPPLIER_NOT_FOUND", "Fournisseur introuvable.");

    private static SupplierDto ToDto(Supplier s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ContactName = s.ContactName,
        Phone = s.Phone,
        Email = s.Email,
        Notes = s.Notes,
        IsActive = s.IsActive
    };
}
