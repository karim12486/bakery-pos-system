namespace Nizam.Api.Core.Enums;

/// <summary>Lifecycle of a table <see cref="Entities.Reservation"/>.</summary>
public enum ReservationStatus
{
    /// <summary>Created, not yet confirmed with the guest.</summary>
    Booked,

    /// <summary>Confirmed (guest acknowledged). Eligible for a reminder.</summary>
    Confirmed,

    /// <summary>Guest arrived and was seated (a TableSession opened).</summary>
    Seated,

    /// <summary>Guest never showed (auto-marked after the slot lapses).</summary>
    NoShow,

    /// <summary>Cancelled by the guest or the restaurant.</summary>
    Cancelled
}
