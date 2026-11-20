using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Drivers;

/// <summary>
/// Paging over the driver roster, with the roster screen's search and sort.
/// <para>
/// The roster used to be read whole and searched in the browser; it is paged like every other list
/// now, and the search and the sort run in SQL over every driver before OFFSET and LIMIT. The
/// pickers that have to offer every driver - the delivery form's assignment, the shift form's driver
/// - read <see cref="IDriverService.ListAllAsync"/> instead, which a table's page cannot cap.
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListDriversQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="DriverSort"/> member's name, or null for the roster's own order. Any other text is
/// refused with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
/// <param name="Search">
/// Text the name, email, licence number or vehicle must contain, without regard to case; blank
/// narrows nothing.
/// </param>
public sealed record ListDriversQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false,
    string? Search = null);

/// <summary>The columns the driver roster can be ordered by.</summary>
public enum DriverSort
{
    /// <summary>Given name.</summary>
    FirstName,

    /// <summary>Family name.</summary>
    LastName,

    /// <summary>Sign-in address.</summary>
    Email,

    /// <summary>Driving licence number.</summary>
    LicenseNumber,

    /// <summary>The assigned vehicle's model. Drivers with none sort to the far end in both directions.</summary>
    Vehicle,

    /// <summary>
    /// The average review score. Drivers nobody has reviewed sort to the far end in both
    /// directions: no verdict is neither the best rating nor the worst.
    /// </summary>
    Rating,
}
