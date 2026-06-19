namespace Nizam.Api.Core.Entities;

/// <summary>
/// One occupancy of a <see cref="Table"/> — opened when guests are seated, closed when they
/// leave (bill settled). Ties together the table, the server who owns it, the guest count,
/// and the active <see cref="Order"/>.
///
/// <para><b>Source of truth for table state</b>: an OPEN session (ClosedAt == null) means the
/// table is Occupied. On close, the table flips to Dirty until a busser clears it (Free).
/// <see cref="Table.Status"/> is a denormalised cache of this for fast floor-plan rendering.</para>
///
/// <para><b>Concurrency</b>: <see cref="RowVersion"/> guards against two servers seating the
/// same empty table simultaneously — the second write loses with a 409 Conflict.</para>
/// </summary>
public class TableSession
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }

    public int TableId { get; set; }
    public Table? Table { get; set; }

    /// <summary>The server (waitstaff user) who opened / owns this session. Null is allowed
    /// for counter-style seating where no specific server is assigned.</summary>
    public int? ServerUserId { get; set; }
    public User? ServerUser { get; set; }

    /// <summary>The active order for this occupancy. Set once the seat operation creates
    /// the order. Nullable because the row is written first, then the order linked.</summary>
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Number of guests seated. Drives covers-per-table reporting.</summary>
    public int GuestCount { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the session ends (bill settled / table vacated). Null while open.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Optional notes captured at seating (e.g. "window seat", "birthday").</summary>
    public string? Notes { get; set; }

    /// <summary>Optimistic-concurrency token. SQL Server rowversion; the in-memory test
    /// provider ignores it. Prevents double-seating races.</summary>
    public byte[]? RowVersion { get; set; }
}
