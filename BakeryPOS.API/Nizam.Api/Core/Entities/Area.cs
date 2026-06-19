namespace Nizam.Api.Core.Entities;

/// <summary>
/// A logical section of a branch's floor — Indoor, Outdoor, Bar, Terrace, Private Room.
/// Used by the floor-plan editor to group tables and by reports to slice metrics per section.
///
/// <para>Per-branch (each branch defines its own areas). Per-tenant scoping is implicit
/// via Branch ownership but explicit on the row for filter-fast queries.</para>
/// </summary>
public class Area
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Display label (e.g. "Indoor", "Terrace", "Private Room").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower sort_order = displayed earlier in the area picker / report breakdown.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag. Inactive areas are hidden from the floor-plan picker
    /// but kept so historical orders referencing tables in those areas still resolve.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Table> Tables { get; set; } = new List<Table>();
}
