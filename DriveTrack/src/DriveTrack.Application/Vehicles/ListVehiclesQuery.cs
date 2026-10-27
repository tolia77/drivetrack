namespace DriveTrack.Application.Vehicles;

/// <summary>
/// NFR-27's paging over the fleet list, with the sort and the search the fleet screen offers.
/// <para>
/// The original's <c>GET /vehicles</c> paged with <c>skip</c> and <c>limit</c> and defaulted them
/// to 0 and 100; this keeps exactly that and adds the half the original was missing - the values are
/// validated before a query is built, so a negative offset or a limit of two million is refused
/// rather than passed through to the database.
/// </para>
/// <para>
/// The sort and the search are applied in SQL over the whole fleet, before OFFSET and LIMIT, so a
/// vehicle past the first page is still found and still sorted into its place, and the total the
/// page carries counts the vehicles the search matched rather than the vehicles on the page.
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListVehiclesQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="VehicleSort"/> member's name, or null for the fleet's own order - oldest first. Any
/// other text is refused with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the sort. The id tie-break follows it.</param>
/// <param name="Search">Text the model or the plate must contain, without regard to case; blank narrows nothing.</param>
public sealed record ListVehiclesQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false,
    string? Search = null);

/// <summary>The columns the fleet list can be ordered by.</summary>
public enum VehicleSort
{
    /// <summary>The make and model.</summary>
    Model,

    /// <summary>The licence plate.</summary>
    LicensePlate,

    /// <summary>How much it carries.</summary>
    CapacityKg,
}
