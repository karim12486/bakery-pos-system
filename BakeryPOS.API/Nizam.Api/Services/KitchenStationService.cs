using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IKitchenStationService
{
    Task<IReadOnlyList<KitchenStationDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<KitchenStationDto?> GetAsync(int id, CancellationToken ct);
    Task<KitchenStationDto> CreateAsync(KitchenStationForCreateDto dto, CancellationToken ct);
    Task<KitchenStationDto> UpdateAsync(int id, KitchenStationForUpdateDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);

    /// <summary>Routes a category's products to a station (or clears the route when null).</summary>
    Task RouteCategoryAsync(int categoryId, CategoryRouteDto dto, CancellationToken ct);
}

public sealed class KitchenStationService : IKitchenStationService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public KitchenStationService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<KitchenStationDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.KitchenStations.AsQueryable();
        if (!includeInactive) query = query.Where(s => s.IsActive);

        var stations = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        return _mapper.Map<List<KitchenStationDto>>(stations);
    }

    public async Task<KitchenStationDto?> GetAsync(int id, CancellationToken ct)
    {
        var station = await _context.KitchenStations.FirstOrDefaultAsync(s => s.Id == id, ct);
        return station == null ? null : _mapper.Map<KitchenStationDto>(station);
    }

    public async Task<KitchenStationDto> CreateAsync(KitchenStationForCreateDto dto, CancellationToken ct)
    {
        if (await _context.KitchenStations.AnyAsync(s => s.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_STATION_NAME_TAKEN",
                $"A kitchen station named '{dto.Name}' already exists.");

        var station = _mapper.Map<KitchenStation>(dto);
        station.IsActive = true;
        station.CreatedAt = DateTime.UtcNow;
        station.UpdatedAt = DateTime.UtcNow;

        _context.KitchenStations.Add(station);
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<KitchenStationDto>(station);
    }

    public async Task<KitchenStationDto> UpdateAsync(int id, KitchenStationForUpdateDto dto, CancellationToken ct)
    {
        var station = await _context.KitchenStations.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_STATION_NOT_FOUND", $"Kitchen station {id} introuvable.");

        if (await _context.KitchenStations.AnyAsync(s => s.Id != id && s.Name == dto.Name, ct))
            throw new DomainConflictException("ERR_STATION_NAME_TAKEN",
                $"A kitchen station named '{dto.Name}' already exists.");

        _mapper.Map(dto, station);
        station.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return _mapper.Map<KitchenStationDto>(station);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var station = await _context.KitchenStations.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_STATION_NOT_FOUND", $"Kitchen station {id} introuvable.");

        // Un-route any categories pointing at this station so they don't dangle, then deactivate.
        // (The FK is SetNull on delete, but we soft-deactivate here, so clear routes explicitly.)
        var routed = await _context.Categories.Where(c => c.KitchenStationId == id).ToListAsync(ct);
        foreach (var c in routed) c.KitchenStationId = null;

        station.IsActive = false;
        station.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task RouteCategoryAsync(int categoryId, CategoryRouteDto dto, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new DomainNotFoundException("ERR_CATEGORY_NOT_FOUND", $"Catégorie {categoryId} introuvable.");

        if (dto.KitchenStationId.HasValue)
        {
            var stationExists = await _context.KitchenStations
                .AnyAsync(s => s.Id == dto.KitchenStationId.Value && s.IsActive, ct);
            if (!stationExists)
                throw new DomainNotFoundException("ERR_STATION_NOT_FOUND",
                    $"Kitchen station {dto.KitchenStationId} introuvable ou inactive.");
        }

        category.KitchenStationId = dto.KitchenStationId;
        await _context.SaveChangesAsync(ct);
    }
}
