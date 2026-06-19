using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ITableService
{
    Task<IReadOnlyList<TableDto>> ListForBranchAsync(int branchId, bool includeInactive, CancellationToken ct);
    Task<TableDto?> GetAsync(int id, CancellationToken ct);
    Task<TableDto> CreateAsync(TableForCreateDto dto, CancellationToken ct);
    Task<TableDto> UpdateAsync(int id, TableForUpdateDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);

    /// <summary>Single-payload floor plan: branch's active areas with their active tables,
    /// ready for the editor to render without follow-up queries.</summary>
    Task<FloorPlanDto> GetFloorPlanAsync(int branchId, CancellationToken ct);
}

public sealed class TableService : ITableService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public TableService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TableDto>> ListForBranchAsync(int branchId, bool includeInactive, CancellationToken ct)
    {
        var query = _context.Tables.Where(t => t.BranchId == branchId);
        if (!includeInactive) query = query.Where(t => t.IsActive);

        var tables = await query.OrderBy(t => t.AreaId).ThenBy(t => t.Name).ToListAsync(ct);
        return _mapper.Map<List<TableDto>>(tables);
    }

    public async Task<TableDto?> GetAsync(int id, CancellationToken ct)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);
        return table == null ? null : _mapper.Map<TableDto>(table);
    }

    public async Task<TableDto> CreateAsync(TableForCreateDto dto, CancellationToken ct)
    {
        await EnsureBranchAndAreaConsistentAsync(dto.BranchId, dto.AreaId, ct);

        if (await _context.Tables.AnyAsync(t => t.BranchId == dto.BranchId && t.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_TABLE_NAME_TAKEN",
                $"A table named '{dto.Name}' already exists in branch {dto.BranchId}.");

        var table = _mapper.Map<Table>(dto);
        table.Status = Core.Enums.TableStatus.Free;
        table.IsActive = true;
        table.CreatedAt = DateTime.UtcNow;
        table.UpdatedAt = DateTime.UtcNow;

        _context.Tables.Add(table);
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<TableDto>(table);
    }

    public async Task<TableDto> UpdateAsync(int id, TableForUpdateDto dto, CancellationToken ct)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", $"Table {id} introuvable.");

        await EnsureBranchAndAreaConsistentAsync(table.BranchId, dto.AreaId, ct);

        if (await _context.Tables.AnyAsync(t => t.Id != id && t.BranchId == table.BranchId && t.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_TABLE_NAME_TAKEN",
                $"A table named '{dto.Name}' already exists in branch {table.BranchId}.");

        _mapper.Map(dto, table);
        table.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<TableDto>(table);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", $"Table {id} introuvable.");

        // Refuse if currently occupied — operator should close the open session first.
        // The TableSession entity (next branch) will own this check more thoroughly;
        // for now we just gate on the denormalised Status field.
        if (table.Status == Core.Enums.TableStatus.Occupied)
            throw new DomainConflictException("ERR_TABLE_OCCUPIED",
                "Cannot deactivate a table that's currently occupied. Close the open session first.");

        table.IsActive = false;
        table.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<FloorPlanDto> GetFloorPlanAsync(int branchId, CancellationToken ct)
    {
        var areas = await _context.Areas
            .Where(a => a.BranchId == branchId && a.IsActive)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var tables = await _context.Tables
            .Where(t => t.BranchId == branchId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var tablesByArea = tables.GroupBy(t => t.AreaId).ToDictionary(g => g.Key, g => g.ToList());

        return new FloorPlanDto
        {
            BranchId = branchId,
            Areas = areas.Select(a => new FloorPlanAreaDto
            {
                Id = a.Id,
                Name = a.Name,
                SortOrder = a.SortOrder,
                IsActive = a.IsActive,
                Tables = tablesByArea.TryGetValue(a.Id, out var list)
                    ? _mapper.Map<List<TableDto>>(list)
                    : Array.Empty<TableDto>(),
            }).ToList(),
        };
    }

    // ----- Helpers -----------------------------------------------------------------

    /// <summary>Validates that (1) the branch exists, (2) the area exists, and (3) the area
    /// belongs to that branch. Prevents cross-branch attachment which would corrupt floor
    /// plans for other branches.</summary>
    private async Task EnsureBranchAndAreaConsistentAsync(int branchId, int areaId, CancellationToken ct)
    {
        if (!await _context.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", $"Branche {branchId} introuvable.");

        var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == areaId, ct)
            ?? throw new DomainNotFoundException("ERR_AREA_NOT_FOUND", $"Area {areaId} introuvable.");

        if (area.BranchId != branchId)
            throw new DomainException("ERR_AREA_BRANCH_MISMATCH",
                $"Area {areaId} belongs to a different branch.");
    }
}
