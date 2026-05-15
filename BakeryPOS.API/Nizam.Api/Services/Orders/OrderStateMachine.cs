using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.Services.Orders;

/// <summary>
/// Validates state transitions for <see cref="Core.Entities.Order"/>. Pure logic — no DB
/// or DI dependencies. Used by services that change order state to guarantee invariants
/// across the Phase A → Phase B evolution.
///
/// <para><b>Transition graph</b>:
/// <list type="bullet">
///   <item>Open → Fired       (sent to kitchen — Phase B dine-in)</item>
///   <item>Open → Served      (skip-kitchen path for cold items / counter-service)</item>
///   <item>Open → Closed      (Phase A counter-service shortcut: paid + done in one action)</item>
///   <item>Open → Cancelled   (voided before any prep)</item>
///   <item>Fired → Served     (kitchen plated; awaiting service)</item>
///   <item>Fired → Cancelled  (voided after fire — manager override expected in Phase B)</item>
///   <item>Served → Closed    (paid)</item>
///   <item>Served → Cancelled (voided after service — manager override expected in Phase B)</item>
/// </list></para>
///
/// <para><b>Terminal states</b>: <see cref="OrderStatus.Closed"/> and
/// <see cref="OrderStatus.Cancelled"/> cannot transition out. Re-opening a closed order
/// (refund / item-add) is a separate concept handled at a higher level by the future
/// refund/comp flow.</para>
///
/// <para><b>Creation</b>: creating an order at any state is allowed; the state machine
/// only governs CHANGES. Phase A code creates orders at <see cref="OrderStatus.Open"/> then
/// transitions to <see cref="OrderStatus.Closed"/> via this service to make the lifecycle
/// explicit and testable.</para>
/// </summary>
public interface IOrderStateMachine
{
    /// <summary>True if <paramref name="from"/> → <paramref name="to"/> is a permitted transition
    /// (or a no-op where <c>from == to</c>).</summary>
    bool CanTransition(OrderStatus from, OrderStatus to);

    /// <summary>Throws <see cref="InvalidOrderTransitionException"/> if the transition isn't allowed.
    /// Same-state no-ops pass silently.</summary>
    void AssertTransition(OrderStatus from, OrderStatus to);
}

public sealed class OrderStateMachine : IOrderStateMachine
{
    // Adjacency map. Each entry: from → set of permitted "to" states.
    // Terminal states (Closed, Cancelled) intentionally absent from the keys.
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> Allowed = new()
    {
        [OrderStatus.Open] = new HashSet<OrderStatus>
        {
            OrderStatus.Fired,
            OrderStatus.Served,
            OrderStatus.Closed,
            OrderStatus.Cancelled,
        },
        [OrderStatus.Fired] = new HashSet<OrderStatus>
        {
            OrderStatus.Served,
            OrderStatus.Cancelled,
        },
        [OrderStatus.Served] = new HashSet<OrderStatus>
        {
            OrderStatus.Closed,
            OrderStatus.Cancelled,
        },
    };

    public bool CanTransition(OrderStatus from, OrderStatus to)
    {
        if (from == to) return true; // no-op
        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public void AssertTransition(OrderStatus from, OrderStatus to)
    {
        if (CanTransition(from, to)) return;
        throw new InvalidOrderTransitionException(from, to);
    }
}
