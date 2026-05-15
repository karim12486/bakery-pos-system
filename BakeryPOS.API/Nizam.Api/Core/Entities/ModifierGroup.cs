namespace Nizam.Api.Core.Entities;

/// <summary>
/// A set of options/customizations attached to one or more products. Each group is a
/// pick-one or pick-many decision the cashier presents — e.g. "Size" (small/medium/large,
/// pick 1), "Milk" (whole/skim/oat, pick 1), "Extras" (sauces/toppings, pick any).
///
/// <para><b>Multi-tenant scoping</b>: tenant-wide (no <c>BranchId</c>) — operators
/// typically share modifier definitions across all their branches. Per-branch overrides
/// are Phase 2+.</para>
///
/// <para><b>Selection rules</b>: <see cref="MinSelect"/> and <see cref="MaxSelect"/>
/// gate the cashier UI and order validation. <c>min=0, max=1</c> = optional single-choice;
/// <c>min=1, max=1</c> = required single-choice; <c>min=0, max=N</c> = optional multi-select;
/// <c>min=1, max=N</c> = required at-least-one. The constraint <c>min ≤ max</c> is
/// enforced at validation time.</para>
/// </summary>
public class ModifierGroup
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Display label shown above the modifier picker (e.g. "Size", "Milk").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional helper text shown beneath the label.</summary>
    public string? Description { get; set; }

    /// <summary>Minimum number of modifiers that MUST be selected from this group. 0 = optional.</summary>
    public int MinSelect { get; set; }

    /// <summary>Maximum number of modifiers that MAY be selected from this group.
    /// 1 = single-choice; greater = multi-select.</summary>
    public int MaxSelect { get; set; } = 1;

    /// <summary>Lower sort_order = displayed earlier in the cashier picker.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag. Inactive groups are hidden from new orders but kept for
    /// historical order replay.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Modifier> Modifiers { get; set; } = new List<Modifier>();
    public ICollection<ProductModifierGroup> ProductLinks { get; set; } = new List<ProductModifierGroup>();
}
