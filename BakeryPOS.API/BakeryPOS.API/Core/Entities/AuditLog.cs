namespace BakeryPOS.API.Core.Entities;

/// <summary>
/// Immutable audit record. Written by services on security/money-relevant events:
/// price changes, refunds, voids, cash variance, permission changes, sign-in failures.
///
/// Each row carries enough context to answer "who did what, when, to which entity"
/// without joining to live tables (those rows may be deleted later).
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }

    /// <summary>The user who performed the action. Null for system / anonymous events.</summary>
    public int? UserId { get; set; }

    /// <summary>Username snapshot — preserved even if the user is later deleted.</summary>
    public string? Username { get; set; }

    /// <summary>Short action code — e.g. <c>sale.create</c>, <c>sale.refund</c>,
    /// <c>product.price_changed</c>, <c>user.permissions_changed</c>, <c>shift.variance</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Entity type affected — e.g. <c>Sale</c>, <c>Product</c>, <c>User</c>.</summary>
    public string? EntityType { get; set; }

    /// <summary>Affected entity id.</summary>
    public int? EntityId { get; set; }

    /// <summary>Free-form JSON or text summary of the relevant before/after values.</summary>
    public string? Details { get; set; }

    /// <summary>Client IP at the time of the action — useful for security investigations.</summary>
    public string? IpAddress { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
