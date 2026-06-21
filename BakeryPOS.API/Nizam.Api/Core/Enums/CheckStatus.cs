namespace Nizam.Api.Core.Enums;

/// <summary>Lifecycle of a single <see cref="Entities.Check"/> (one printed bill).</summary>
public enum CheckStatus
{
    /// <summary>Awaiting payment.</summary>
    Open,

    /// <summary>Settled.</summary>
    Paid,

    /// <summary>Cancelled before payment (e.g. re-split).</summary>
    Voided
}

/// <summary>How an order's total is divided into checks.</summary>
public enum SplitMode
{
    /// <summary>One check for the whole order.</summary>
    Single,

    /// <summary>Each check owns specific order items (drag items between checks).</summary>
    ByItem,

    /// <summary>Total divided into N equal checks (no per-item assignment).</summary>
    Equal,

    /// <summary>Total divided by explicit percentages.</summary>
    Percentage
}
