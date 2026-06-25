namespace Nizam.Api.Core.Enums;

/// <summary>Lifecycle of a purchase order (Phase 3.7 / inventory_ops).</summary>
public enum PurchaseOrderStatus
{
    /// <summary>Being assembled; lines still editable; no stock impact.</summary>
    Draft,

    /// <summary>Sent to the supplier; awaiting delivery; lines frozen.</summary>
    Submitted,

    /// <summary>Goods received; stock has been incremented from the received quantities.</summary>
    Received,

    /// <summary>Abandoned before receipt; no stock impact.</summary>
    Cancelled
}

/// <summary>Lifecycle of a branch-to-branch stock transfer (Phase 3.7 / inventory_ops).</summary>
public enum StockTransferStatus
{
    /// <summary>Being assembled at the source branch; editable; no stock impact.</summary>
    Draft,

    /// <summary>Dispatched from the source branch.</summary>
    Sent,

    /// <summary>Confirmed received at the destination branch.</summary>
    Received,

    /// <summary>Abandoned; no stock impact.</summary>
    Cancelled
}

/// <summary>Why stock was written off in the waste log (Phase 3.7 / inventory_ops).</summary>
public enum WasteReason
{
    Spoilage,
    Expiry,
    Breakage,
    Theft,
    PrepError,
    Other
}
