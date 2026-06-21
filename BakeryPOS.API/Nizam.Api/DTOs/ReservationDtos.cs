using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class ReservationDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int PartySize { get; set; }
    public DateTime ReservedFor { get; set; }
    public int? TableId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ReminderSentAt { get; set; }
}

public class ReservationForCreateDto
{
    [Required, Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Range(1, 100)]
    public int PartySize { get; set; } = 2;

    [Required]
    public DateTime ReservedFor { get; set; }

    public int? TableId { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

/// <summary>Seat a reservation now — opens a table session for the booked party.</summary>
public class SeatReservationDto
{
    /// <summary>Table to seat at. Falls back to the reservation's pre-assigned table when null.</summary>
    public int? TableId { get; set; }
}
