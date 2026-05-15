using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IModifierGroupService
{
    Task<IReadOnlyList<ModifierGroupDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<ModifierGroupDto?> GetAsync(int id, CancellationToken ct);
    Task<ModifierGroupDto> CreateAsync(ModifierGroupForCreateDto dto, CancellationToken ct);
    Task<ModifierGroupDto> UpdateAsync(int id, ModifierGroupForUpdateDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);

    Task<ModifierDto> AddModifierAsync(int groupId, ModifierForCreateDto dto, CancellationToken ct);
    Task<ModifierDto> UpdateModifierAsync(int groupId, int modifierId, ModifierForUpdateDto dto, CancellationToken ct);
    Task DeactivateModifierAsync(int groupId, int modifierId, CancellationToken ct);

    Task<IReadOnlyList<ModifierGroupDto>> ListForProductAsync(int productId, CancellationToken ct);
    Task AttachToProductAsync(int productId, ProductModifierGroupAttachDto dto, CancellationToken ct);
    Task DetachFromProductAsync(int productId, int groupId, CancellationToken ct);
}

public sealed class ModifierGroupService : IModifierGroupService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IValidator<ModifierGroupForCreateDto> _createValidator;
    private readonly IValidator<ModifierGroupForUpdateDto> _updateValidator;

    public ModifierGroupService(
        AppDbContext context,
        IMapper mapper,
        IValidator<ModifierGroupForCreateDto> createValidator,
        IValidator<ModifierGroupForUpdateDto> updateValidator)
    {
        _context = context;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    // ----- Groups --------------------------------------------------------------------

    public async Task<IReadOnlyList<ModifierGroupDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.ModifierGroups.Include(g => g.Modifiers).AsQueryable();
        if (!includeInactive) query = query.Where(g => g.IsActive);

        var groups = await query
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
            .ToListAsync(ct);
        return _mapper.Map<List<ModifierGroupDto>>(groups);
    }

    public async Task<ModifierGroupDto?> GetAsync(int id, CancellationToken ct)
    {
        var group = await _context.ModifierGroups
            .Include(g => g.Modifiers)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        return group == null ? null : _mapper.Map<ModifierGroupDto>(group);
    }

    public async Task<ModifierGroupDto> CreateAsync(ModifierGroupForCreateDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);

        // Tenant-unique name (enforced by the DB index too — early check gives a clean error).
        if (await _context.ModifierGroups.AnyAsync(g => g.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_MODIFIER_GROUP_NAME_TAKEN",
                $"A modifier group named '{dto.Name}' already exists.");

        var group = _mapper.Map<ModifierGroup>(dto);
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        group.IsActive = true;

        _context.ModifierGroups.Add(group);
        await _context.SaveChangesAsync(ct);

        // Re-fetch with includes for a consistent DTO shape.
        return (await GetAsync(group.Id, ct))!;
    }

    public async Task<ModifierGroupDto> UpdateAsync(int id, ModifierGroupForUpdateDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);

        var group = await _context.ModifierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_MODIFIER_GROUP_NOT_FOUND",
                $"Modifier group {id} introuvable.");

        // Tenant-unique name (excluding self).
        if (await _context.ModifierGroups.AnyAsync(g => g.Id != id && g.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_MODIFIER_GROUP_NAME_TAKEN",
                $"A modifier group named '{dto.Name}' already exists.");

        _mapper.Map(dto, group);
        group.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return (await GetAsync(group.Id, ct))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var group = await _context.ModifierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_MODIFIER_GROUP_NOT_FOUND",
                $"Modifier group {id} introuvable.");

        // Soft-deactivate. The group stays in the DB so historical orders that reference its
        // modifiers (once OrderItemModifier wiring lands) keep their snapshot intact.
        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    // ----- Modifiers within a group --------------------------------------------------

    public async Task<ModifierDto> AddModifierAsync(int groupId, ModifierForCreateDto dto, CancellationToken ct)
    {
        var group = await _context.ModifierGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new DomainNotFoundException("ERR_MODIFIER_GROUP_NOT_FOUND",
                $"Modifier group {groupId} introuvable.");

        var modifier = _mapper.Map<Modifier>(dto);
        modifier.ModifierGroupId = group.Id;
        modifier.IsActive = true;
        modifier.CreatedAt = DateTime.UtcNow;

        _context.Modifiers.Add(modifier);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ModifierDto>(modifier);
    }

    public async Task<ModifierDto> UpdateModifierAsync(int groupId, int modifierId, ModifierForUpdateDto dto,
        CancellationToken ct)
    {
        var modifier = await _context.Modifiers
            .FirstOrDefaultAsync(m => m.Id == modifierId && m.ModifierGroupId == groupId, ct)
            ?? throw new DomainNotFoundException("ERR_MODIFIER_NOT_FOUND",
                $"Modifier {modifierId} not found in group {groupId}.");

        _mapper.Map(dto, modifier);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ModifierDto>(modifier);
    }

    public async Task DeactivateModifierAsync(int groupId, int modifierId, CancellationToken ct)
    {
        var modifier = await _context.Modifiers
            .FirstOrDefaultAsync(m => m.Id == modifierId && m.ModifierGroupId == groupId, ct)
            ?? throw new DomainNotFoundException("ERR_MODIFIER_NOT_FOUND",
                $"Modifier {modifierId} not found in group {groupId}.");

        modifier.IsActive = false;
        await _context.SaveChangesAsync(ct);
    }

    // ----- Product attach ------------------------------------------------------------

    public async Task<IReadOnlyList<ModifierGroupDto>> ListForProductAsync(int productId, CancellationToken ct)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == productId, ct))
            throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", $"Produit {productId} introuvable.");

        var links = await _context.ProductModifierGroups
            .Where(l => l.ProductId == productId)
            .Include(l => l.ModifierGroup!).ThenInclude(g => g.Modifiers)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .ToListAsync(ct);

        return links
            .Where(l => l.ModifierGroup != null)
            .Select(l => _mapper.Map<ModifierGroupDto>(l.ModifierGroup!))
            .ToList();
    }

    public async Task AttachToProductAsync(int productId, ProductModifierGroupAttachDto dto, CancellationToken ct)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == productId, ct))
            throw new DomainNotFoundException("ERR_PRODUCT_NOT_FOUND", $"Produit {productId} introuvable.");

        if (!await _context.ModifierGroups.AnyAsync(g => g.Id == dto.ModifierGroupId, ct))
            throw new DomainNotFoundException("ERR_MODIFIER_GROUP_NOT_FOUND",
                $"Modifier group {dto.ModifierGroupId} introuvable.");

        // Idempotent: silently no-op if already attached.
        var existing = await _context.ProductModifierGroups
            .FirstOrDefaultAsync(l => l.ProductId == productId && l.ModifierGroupId == dto.ModifierGroupId, ct);
        if (existing != null)
        {
            if (existing.SortOrder != dto.SortOrder)
            {
                existing.SortOrder = dto.SortOrder;
                await _context.SaveChangesAsync(ct);
            }
            return;
        }

        _context.ProductModifierGroups.Add(new ProductModifierGroup
        {
            ProductId = productId,
            ModifierGroupId = dto.ModifierGroupId,
            SortOrder = dto.SortOrder,
        });
        await _context.SaveChangesAsync(ct);
    }

    public async Task DetachFromProductAsync(int productId, int groupId, CancellationToken ct)
    {
        var link = await _context.ProductModifierGroups
            .FirstOrDefaultAsync(l => l.ProductId == productId && l.ModifierGroupId == groupId, ct);
        if (link == null) return; // idempotent — no-op if already detached

        _context.ProductModifierGroups.Remove(link);
        await _context.SaveChangesAsync(ct);
    }
}
