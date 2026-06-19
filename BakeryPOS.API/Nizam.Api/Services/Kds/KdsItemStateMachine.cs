using Nizam.Api.Core.Entities;

namespace Nizam.Api.Services.Kds;

/// <summary>
/// Pure-logic transition rules for an <see cref="OrderItem"/>'s kitchen lifecycle. Mirrors the
/// shape of the order-level <c>OrderStateMachine</c> but governs the KDS sub-states.
///
/// <para>Graph (terminal: Served, Voided, Closed):
/// <list type="bullet">
///   <item>Pending → Fired</item>
///   <item>Fired → Cooking | Ready | Served | Voided</item>
///   <item>Cooking → Ready | Served | Voided</item>
///   <item>Ready → Served | Voided</item>
/// </list>
/// "Fired → Served" and "Cooking → Served" exist for kitchens that skip the Ready bump
/// (hand straight to the runner). Same-state is a no-op.</para>
/// </summary>
public static class KdsItemStateMachine
{
    private static readonly Dictionary<OrderItemStatus, HashSet<OrderItemStatus>> Allowed = new()
    {
        [OrderItemStatus.Pending] = new() { OrderItemStatus.Fired, OrderItemStatus.Voided },
        [OrderItemStatus.Fired]   = new() { OrderItemStatus.Cooking, OrderItemStatus.Ready, OrderItemStatus.Served, OrderItemStatus.Voided },
        [OrderItemStatus.Cooking] = new() { OrderItemStatus.Ready, OrderItemStatus.Served, OrderItemStatus.Voided },
        [OrderItemStatus.Ready]   = new() { OrderItemStatus.Served, OrderItemStatus.Voided },
    };

    public static bool CanTransition(OrderItemStatus from, OrderItemStatus to)
    {
        if (from == to) return true;
        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }
}
