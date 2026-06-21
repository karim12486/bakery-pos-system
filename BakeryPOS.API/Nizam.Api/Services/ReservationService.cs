using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IReservationService
{
    Task<IReadOnlyList<ReservationDto>> ListForBranchAsync(int branchId, DateTime? onDate, CancellationToken ct);
    Task<ReservationDto?> GetAsync(int id, CancellationToken ct);
    Task<ReservationDto> CreateAsync(ReservationForCreateDto dto, CancellationToken ct);
    Task<ReservationDto> ConfirmAsync(int id, CancellationToken ct);
    Task<ReservationDto> CancelAsync(int id, CancellationToken ct);
    Task<ReservationDto> MarkNoShowAsync(int id, CancellationToken ct);

    /// <summary>Seats the reservation's party — opens a TableSession (+ dine-in order) via the
    /// dine-in service and marks the reservation Seated.</summary>
    Task<TableSessionDto> SeatAsync(int id, SeatReservationDto dto, string actingUsername, CancellationToken ct);
}

public sealed class ReservationService : IReservationService
{
    private readonly AppDbContext _context;
    private readonly IDineInService _dineIn;

    public ReservationService(AppDbContext context, IDineInService dineIn)
    {
        _context = context;
        _dineIn = dineIn;
    }

    public async Task<IReadOnlyList<ReservationDto>> ListForBranchAsync(int branchId, DateTime? onDate, CancellationToken ct)
    {
        var query = _context.Reservations.Where(r => r.BranchId == branchId);
        if (onDate.HasValue)
        {
            var dayStart = onDate.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(r => r.ReservedFor >= dayStart && r.ReservedFor < dayEnd);
        }
        var rows = await query.OrderBy(r => r.ReservedFor).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ReservationDto?> GetAsync(int id, CancellationToken ct)
    {
        var r = await _context.Reservations.FirstOrDefaultAsync(x => x.Id == id, ct);
        return r == null ? null : ToDto(r);
    }

    public async Task<ReservationDto> CreateAsync(ReservationForCreateDto dto, CancellationToken ct)
    {
        if (!await _context.Branches.AnyAsync(b => b.Id == dto.BranchId, ct))
            throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", $"Branche {dto.BranchId} introuvable.");

        if (dto.TableId is int tableId &&
            !await _context.Tables.AnyAsync(t => t.Id == tableId && t.BranchId == dto.BranchId, ct))
            throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND",
                $"Table {tableId} introuvable dans la branche {dto.BranchId}.");

        var reservation = new Reservation
        {
            BranchId = dto.BranchId,
            CustomerName = dto.CustomerName,
            Phone = dto.Phone,
            PartySize = dto.PartySize,
            ReservedFor = dto.ReservedFor,
            TableId = dto.TableId,
            Notes = dto.Notes,
            Status = ReservationStatus.Booked,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync(ct);
        return ToDto(reservation);
    }

    public Task<ReservationDto> ConfirmAsync(int id, CancellationToken ct)
        => TransitionAsync(id, ReservationStatus.Confirmed, fromBookedOnly: true, ct);

    public Task<ReservationDto> CancelAsync(int id, CancellationToken ct)
        => TransitionAsync(id, ReservationStatus.Cancelled, fromBookedOnly: false, ct);

    public Task<ReservationDto> MarkNoShowAsync(int id, CancellationToken ct)
        => TransitionAsync(id, ReservationStatus.NoShow, fromBookedOnly: false, ct);

    public async Task<TableSessionDto> SeatAsync(int id, SeatReservationDto dto, string actingUsername, CancellationToken ct)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_RESERVATION_NOT_FOUND", $"Reservation {id} introuvable.");

        if (reservation.Status is ReservationStatus.Seated or ReservationStatus.Cancelled or ReservationStatus.NoShow)
            throw new DomainConflictException("ERR_RESERVATION_NOT_SEATABLE",
                $"A reservation in status '{reservation.Status}' cannot be seated.");

        var tableId = dto.TableId ?? reservation.TableId
            ?? throw new DomainException("ERR_TABLE_REQUIRED",
                "No table specified and the reservation has no pre-assigned table.");

        // Delegate to the dine-in seating flow (opens session + order, flips table to Occupied).
        var session = await _dineIn.SeatAsync(
            new SeatGuestsDto { TableId = tableId, GuestCount = reservation.PartySize, Notes = reservation.Notes },
            actingUsername, ct);

        reservation.Status = ReservationStatus.Seated;
        reservation.TableId = tableId;
        await _context.SaveChangesAsync(ct);

        return session;
    }

    private async Task<ReservationDto> TransitionAsync(int id, ReservationStatus to, bool fromBookedOnly, CancellationToken ct)
    {
        var r = await _context.Reservations.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_RESERVATION_NOT_FOUND", $"Reservation {id} introuvable.");

        if (r.Status is ReservationStatus.Seated or ReservationStatus.Cancelled or ReservationStatus.NoShow)
            throw new DomainConflictException("ERR_RESERVATION_FINALIZED",
                $"A reservation in status '{r.Status}' can no longer change.");
        if (fromBookedOnly && r.Status != ReservationStatus.Booked)
            throw new DomainConflictException("ERR_RESERVATION_NOT_BOOKED",
                "Only a booked reservation can be confirmed.");

        r.Status = to;
        await _context.SaveChangesAsync(ct);
        return ToDto(r);
    }

    private static ReservationDto ToDto(Reservation r) => new()
    {
        Id = r.Id,
        BranchId = r.BranchId,
        CustomerName = r.CustomerName,
        Phone = r.Phone,
        PartySize = r.PartySize,
        ReservedFor = r.ReservedFor,
        TableId = r.TableId,
        Notes = r.Notes,
        Status = r.Status.ToString(),
        ReminderSentAt = r.ReminderSentAt,
    };
}
