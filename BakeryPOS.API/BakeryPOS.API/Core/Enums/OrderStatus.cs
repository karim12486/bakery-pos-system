namespace BakeryPOS.API.Core.Enums;

/// <summary>
/// Order lifecycle. In Phase A (counter-service), orders are opened + paid + closed in
/// one screen action, so they jump straight to <see cref="Closed"/>. The intermediate
/// states (<see cref="Open"/>, <see cref="Fired"/>, <see cref="Served"/>) are reserved
/// for Phase B (table-service restaurants) where items are taken over time, fired to
/// the kitchen, served, and finally paid for.
///
/// Storing all states from day 1 prevents a schema migration when Phase B lands.
/// </summary>
public enum OrderStatus
{
    /// <summary>Started, items being added. Phase B.</summary>
    Open,

    /// <summary>Sent to kitchen / fulfilment. Phase B.</summary>
    Fired,

    /// <summary>All items served / fulfilled, awaiting payment. Phase B.</summary>
    Served,

    /// <summary>Paid and closed.</summary>
    Closed,

    /// <summary>Voided before close.</summary>
    Cancelled
}
