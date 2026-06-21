using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A table booking — guest details + the requested time + optional table assignment. The
/// reservation reminder job nudges the guest ahead of the slot and auto-marks no-shows.
/// </summary>
public class Reservation
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int PartySize { get; set; }

    /// <summary>The requested seating time (UTC).</summary>
    public DateTime ReservedFor { get; set; }

    /// <summary>Optional pre-assigned table (may be assigned later, at seating).</summary>
    public int? TableId { get; set; }
    public Table? Table { get; set; }

    public string? Notes { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Booked;

    /// <summary>Set when the reminder job has nudged the guest, so we don't double-send.</summary>
    public DateTime? ReminderSentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
