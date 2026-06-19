using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>One ticket line on a KDS screen.</summary>
public class KdsItemDto
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int? TableId { get; set; }
    public int? KitchenStationId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Modifier names for the kitchen to read (e.g. "Oat milk", "Extra shot").</summary>
    public IReadOnlyList<string> Modifiers { get; set; } = Array.Empty<string>();

    public string? Notes { get; set; }
    public DateTime? FiredAt { get; set; }

    /// <summary>Seconds since fired (0 if not yet fired). Drives the aging color client-side too.</summary>
    public int AgeSeconds { get; set; }

    /// <summary>Server-computed aging bucket: <c>green</c> (0–5m), <c>yellow</c> (5–10m),
    /// <c>red</c> (10m+). Sent so all screens agree regardless of clock skew.</summary>
    public string AgeColor { get; set; } = "green";
}

/// <summary>Payload for voiding a kitchen item (reason required for the audit trail).</summary>
public class VoidKdsItemDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}
