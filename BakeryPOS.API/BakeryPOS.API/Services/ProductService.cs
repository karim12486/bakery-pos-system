using AutoMapper;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IProductService
{
    Task<PagedResponse<ProductDto>> ListAsync(int? categoryId, string? search, PaginationParams pagination, CancellationToken ct);
    Task<ProductDto?> GetAsync(int id, CancellationToken ct);
    Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct);
    Task<ProductDto> CreateAsync(ProductForCreateDto dto, CancellationToken ct);
    Task UpdateAsync(int id, ProductForUpdateDto dto, CancellationToken ct);
    Task SoftDeleteAsync(int id, CancellationToken ct);
}

public sealed class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ProductService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResponse<ProductDto>> ListAsync(int? categoryId, string? search, PaginationParams pagination, CancellationToken ct)
    {
        var query = _context.Products.Include(p => p.Category).Where(p => p.IsActive);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));
        }

        var totalRecords = await query.CountAsync(ct);
        var products = await query
            .OrderBy(p => p.Name)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<ProductDto>(_mapper.Map<IEnumerable<ProductDto>>(products),
            pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<ProductDto?> GetAsync(int id, CancellationToken ct)
    {
        var product = await _context.Products.FindAsync(new object?[] { id }, ct);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.IsActive && p.Barcode == barcode, ct);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(ProductForCreateDto dto, CancellationToken ct)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new DomainException("ERR_CATEGORY_NOT_FOUND", $"Catégorie {dto.CategoryId} introuvable.");

        if (!string.IsNullOrEmpty(dto.Barcode) &&
            await _context.Products.AnyAsync(p => p.IsActive && p.Barcode == dto.Barcode, ct))
        {
            throw new DomainConflictException("ERR_BARCODE_DUPLICATE",
                $"Le code-barres '{dto.Barcode}' est déjà utilisé par un autre produit.");
        }

        var newProduct = _mapper.Map<Product>(dto);
        newProduct.IsActive = true;
        newProduct.CreatedAt = DateTime.UtcNow;

        await _context.Products.AddAsync(newProduct, ct);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ProductDto>(newProduct);
    }

    public async Task UpdateAsync(int id, ProductForUpdateDto dto, CancellationToken ct)
    {
        var product = await _context.Products.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", $"Produit {id} introuvable.");

        if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new DomainException("ERR_CATEGORY_NOT_FOUND", $"Catégorie {dto.CategoryId} introuvable.");

        if (!string.IsNullOrEmpty(dto.Barcode) &&
            await _context.Products.AnyAsync(p => p.IsActive && p.Id != id && p.Barcode == dto.Barcode, ct))
        {
            throw new DomainConflictException("ERR_BARCODE_DUPLICATE",
                $"Le code-barres '{dto.Barcode}' est déjà utilisé par un autre produit.");
        }

        _mapper.Map(dto, product);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct)
    {
        var product = await _context.Products.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", $"Produit {id} introuvable.");

        // Soft delete preserves historical sales referencing this product.
        product.IsActive = false;
        await _context.SaveChangesAsync(ct);
    }
}
