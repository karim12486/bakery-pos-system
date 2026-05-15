using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

/// <summary>
/// Validates a set of modifier selections against the modifier groups attached to a product,
/// then materializes <see cref="OrderItemModifier"/> snapshot rows. Pure domain logic — no
/// EF tracking changes; the caller decides when to <c>Add</c> the rows and <c>SaveChanges</c>.
///
/// <para><b>Validation rules</b> (throws <see cref="DomainException"/> on first failure):
/// <list type="bullet">
///   <item>Every selected modifier id exists and belongs to the tenant.</item>
///   <item>Every selected modifier's parent group is attached to the product.</item>
///   <item>Each group's selection count is within [<c>MinSelect</c>, <c>MaxSelect</c>].</item>
///   <item>Inactive modifier groups / modifiers can't be picked on new orders.</item>
/// </list></para>
///
/// <para><b>Pricing</b>: <see cref="UnitPriceWithModifiers"/> = product base price +
/// sum of selected modifier <c>PriceDelta</c>. Quantity is applied by the caller at the
/// line-total level.</para>
/// </summary>
public interface IModifierApplicationService
{
    /// <summary>Validates the selection and returns hydrated context for line-total math
    /// + snapshot row creation. Throws <see cref="DomainException"/> on validation failure.</summary>
    Task<ModifierApplicationResult> PrepareAsync(int productId, IReadOnlyCollection<int> modifierIds,
        CancellationToken ct);
}

/// <summary>Result of <see cref="IModifierApplicationService.PrepareAsync"/>. Carries the
/// per-unit price contribution and pre-built snapshot rows for the caller to attach to
/// a freshly-created <see cref="OrderItem"/>.</summary>
public sealed class ModifierApplicationResult
{
    /// <summary>Sum of selected modifier <c>PriceDelta</c>. Per-unit; caller multiplies by quantity.</summary>
    public decimal TotalPriceDelta { get; init; }

    /// <summary>Materialised snapshot rows. Caller sets <c>OrderItemId</c> after the
    /// parent <see cref="OrderItem"/> gets its identity, or assigns via the navigation property.</summary>
    public IReadOnlyList<OrderItemModifier> Snapshots { get; init; } = Array.Empty<OrderItemModifier>();
}

public sealed class ModifierApplicationService : IModifierApplicationService
{
    private readonly AppDbContext _context;

    public ModifierApplicationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ModifierApplicationResult> PrepareAsync(int productId, IReadOnlyCollection<int> modifierIds,
        CancellationToken ct)
    {
        modifierIds ??= Array.Empty<int>();

        // Load every group attached to the product (active ones only — inactive groups
        // shouldn't appear on new orders even if the cashier sent their ids).
        var attachedGroups = await _context.ProductModifierGroups
            .Where(l => l.ProductId == productId)
            .Include(l => l.ModifierGroup!).ThenInclude(g => g.Modifiers)
            .Select(l => l.ModifierGroup!)
            .Where(g => g.IsActive)
            .ToListAsync(ct);

        var attachedGroupIds = attachedGroups.Select(g => g.Id).ToHashSet();

        // Index modifiers by id, restricted to attached + active groups + active modifiers.
        // Anything outside this set is rejected — protects against cross-tenant id leaks,
        // cross-product picks, and stale selections after a modifier was deactivated.
        var validModifiers = attachedGroups
            .SelectMany(g => g.Modifiers.Where(m => m.IsActive).Select(m => (Group: g, Mod: m)))
            .ToDictionary(x => x.Mod.Id);

        // 1. Every requested id must be a valid pick.
        var distinctIds = modifierIds.Distinct().ToList();
        foreach (var id in distinctIds)
        {
            if (!validModifiers.ContainsKey(id))
                throw new DomainException("ERR_MODIFIER_NOT_AVAILABLE",
                    $"Modifier {id} isn't available for product {productId}.");
        }

        // 2. Per-group selection counts must be within [MinSelect, MaxSelect].
        var picksByGroup = distinctIds
            .Select(id => validModifiers[id])
            .GroupBy(x => x.Group.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var group in attachedGroups)
        {
            picksByGroup.TryGetValue(group.Id, out var picked);

            if (picked < group.MinSelect)
                throw new DomainException("ERR_MODIFIER_MIN_NOT_MET",
                    $"Group '{group.Name}' requires at least {group.MinSelect} selection(s); got {picked}.");

            if (picked > group.MaxSelect)
                throw new DomainException("ERR_MODIFIER_MAX_EXCEEDED",
                    $"Group '{group.Name}' allows at most {group.MaxSelect} selection(s); got {picked}.");
        }

        // 3. Build the snapshot rows + sum the delta.
        var snapshots = new List<OrderItemModifier>(distinctIds.Count);
        decimal totalDelta = 0;

        foreach (var id in distinctIds)
        {
            var (group, mod) = validModifiers[id];
            totalDelta += mod.PriceDelta;
            snapshots.Add(new OrderItemModifier
            {
                ModifierId = mod.Id,
                ModifierGroupId = group.Id,
                GroupName = group.Name,
                Name = mod.Name,
                PriceDelta = mod.PriceDelta,
            });
        }

        return new ModifierApplicationResult
        {
            TotalPriceDelta = totalDelta,
            Snapshots = snapshots,
        };
    }
}
