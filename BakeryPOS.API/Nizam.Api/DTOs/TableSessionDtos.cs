using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class TableSessionDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int? ServerUserId { get; set; }
    public string? ServerName { get; set; }
    public int? OrderId { get; set; }
    public int GuestCount { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Payload for seating guests at a table — opens a session and a dine-in order.</summary>
public class SeatGuestsDto
{
    [Required, Range(1, int.MaxValue)]
    public int TableId { get; set; }

    [Range(1, 100)]
    public int GuestCount { get; set; } = 1;

    /// <summary>Optional server (waitstaff) assignment. Defaults to the acting user when null.</summary>
    public int? ServerUserId { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

/// <summary>Payload for transferring an open session to a different table.</summary>
public class TransferTableDto
{
    [Required, Range(1, int.MaxValue)]
    public int ToTableId { get; set; }
}

/// <summary>Payload for merging a session into another (destination keeps its session/order).</summary>
public class MergeSessionDto
{
    [Required, Range(1, int.MaxValue)]
    public int IntoSessionId { get; set; }
}
