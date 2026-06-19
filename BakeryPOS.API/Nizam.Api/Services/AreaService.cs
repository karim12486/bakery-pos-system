using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IAreaService
{
    Task<IReadOnlyList<AreaDto>> ListForBranchAsync(int branchId, bool includeInactive, CancellationToken ct);
    Task<AreaDto?> GetAsync(int id, CancellationToken ct);
    Task<AreaDto> CreateAsync(AreaForCreateDto dto, CancellationToken ct);
    Task<AreaDto> UpdateAsync(int id, AreaForUpdateDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);
}

public sealed class AreaService : IAreaService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public AreaService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AreaDto>> ListForBranchAsync(int branchId, bool includeInactive, CancellationToken ct)
    {
        var query = _context.Areas.Where(a => a.BranchId == branchId);
        if (!includeInactive) query = query.Where(a => a.IsActive);

        var areas = await query
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
            .ToListAsync(ct);
        return _mapper.Map<List<AreaDto>>(areas);
    }

    public async Task<AreaDto?> GetAsync(int id, CancellationToken ct)
    {
        var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);
        return area == null ? null : _mapper.Map<AreaDto>(area);
    }

    public async Task<AreaDto> CreateAsync(AreaForCreateDto dto, CancellationToken ct)
    {
        if (!await _context.Branches.AnyAsync(b => b.Id == dto.BranchId, ct))
            throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", $"Branche {dto.BranchId} introuvable.");

        if (await _context.Areas.AnyAsync(a => a.BranchId == dto.BranchId && a.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_AREA_NAME_TAKEN",
                $"An area named '{dto.Name}' already exists in branch {dto.BranchId}.");

        var area = _mapper.Map<Area>(dto);
        area.IsActive = true;
        area.CreatedAt = DateTime.UtcNow;
        area.UpdatedAt = DateTime.UtcNow;

        _context.Areas.Add(area);
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<AreaDto>(area);
    }

    public async Task<AreaDto> UpdateAsync(int id, AreaForUpdateDto dto, CancellationToken ct)
    {
        var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_AREA_NOT_FOUND", $"Area {id} introuvable.");

        if (await _context.Areas.AnyAsync(a => a.Id != id && a.BranchId == area.BranchId && a.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_AREA_NAME_TAKEN",
                $"An area named '{dto.Name}' already exists in branch {area.BranchId}.");

        _mapper.Map(dto, area);
        area.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<AreaDto>(area);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_AREA_NOT_FOUND", $"Area {id} introuvable.");

        // Refuse if the area still has active tables — operator must move or deactivate
        // them first. Prevents orphaning tables with a non-existent area display name.
        var hasActiveTables = await _context.Tables.AnyAsync(t => t.AreaId == id && t.IsActive, ct);
        if (hasActiveTables)
            throw new DomainConflictException("ERR_AREA_HAS_TABLES",
                "Cannot deactivate an area that still contains active tables. Move or deactivate the tables first.");

        area.IsActive = false;
        area.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
}
