using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// Paging over a shift list, with the sort and the driver-name search the shift screen
/// offers, all applied in SQL before OFFSET and LIMIT.
/// <para>
/// <see cref="DriverId"/> is a filter dispatch may set, never the authorization decision. A driver's
/// narrowing comes from <c>IAccessGuard.RequireShiftScope</c> and overrides whatever the query
/// asked for, so a driver cannot widen their own listing by naming somebody else - and the
/// total the page carries counts that same narrowed set.
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListShiftsQueryValidator.MaximumLimit"/>.</param>
/// <param name="DriverId">The driver row id to narrow to, or null for every driver.</param>
/// <param name="Sort">
/// A <see cref="ShiftSort"/> member's name, or null for newest first. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
/// <param name="Search">Text the driver's name must contain, without regard to case; blank narrows nothing.</param>
/// <param name="OpenOnly">
/// True to list only shifts still running. A driver's own screen asks this to find the shift it
/// offers to end, which is not necessarily on the page of history it is showing.
/// </param>
public sealed record ListShiftsQuery(
    int Offset,
    int Limit,
    int? DriverId,
    string? Sort = null,
    bool Descending = false,
    string? Search = null,
    bool OpenOnly = false);

/// <summary>The columns a shift list can be ordered by.</summary>
public enum ShiftSort
{
    /// <summary>The driver's name, as the row shows it.</summary>
    Driver,

    /// <summary>When the shift started.</summary>
    StartedAt,

    /// <summary>When it ended. Shifts still running sort to the far end in both directions.</summary>
    EndedAt,
}
