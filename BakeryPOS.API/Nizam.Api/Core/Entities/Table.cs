using System.ComponentModel.DataAnnotations.Schema;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A single table in a branch's floor plan. Stored with x/y/width/height coordinates so
/// the editor can render the floor exactly as the owner laid it out.
///
/// <para><b>Status caching</b>: <see cref="Status"/> denormalises the table's current
/// occupancy for fast floor-plan rendering. The real source of truth is the
/// <c>TableSession</c> table (next branch) — open session = Occupied; closed session +
/// not yet bussed = Dirty; cleared = Free. Reserved is set independently by the
/// reservations flow.</para>
/// </summary>
public class Table
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    /// <summary>Display label / number (e.g. "T-12", "Patio 3"). Unique per branch.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Maximum number of guests this table seats.</summary>
    public int Capacity { get; set; }

    public TableShape Shape { get; set; } = TableShape.Square;

    /// <summary>X position on the floor-plan canvas (0 = left). Units are arbitrary —
    /// frontend scales to canvas size. Stored as decimal for precision under scaling.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal X { get; set; }

    /// <summary>Y position on the floor-plan canvas (0 = top).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Y { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Width { get; set; } = 100m;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Height { get; set; } = 100m;

    /// <summary>Rotation in degrees clockwise (0 = upright). Tables can rotate freely on
    /// the canvas — useful for visualising L-shaped sections or angled bars.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal Rotation { get; set; }

    public TableStatus Status { get; set; } = TableStatus.Free;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
