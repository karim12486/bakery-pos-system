namespace Nizam.Api.Core.Enums;

/// <summary>
/// Display state of a table on the floor plan. Free is the default; Occupied gets set
/// when a TableSession opens; Reserved is for confirmed bookings ahead of seating; Dirty
/// is set on bill-close until a busser marks it clean.
///
/// <para>Stored as a string in the DB for readability; the TableSession entity (next branch)
/// is the source of truth for transitions, this field is a denormalized cache for fast
/// floor-plan rendering.</para>
/// </summary>
public enum TableStatus
{
    Free,
    Occupied,
    Reserved,
    Dirty,
}
