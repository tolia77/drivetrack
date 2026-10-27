using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// NFR-27's paging over a delivery list, with the dispatch board's sort and its ten filters (FR-20,
/// FR-101), all answered by the database over every row the caller may see.
/// <para>
/// The filters and the sort are applied in SQL after the access scope and before OFFSET and LIMIT,
/// so a delivery past the first page is still found by a filter and still sorted into its place,
/// and the total the page carries counts what matched rather than what fits. Every order ends on the
/// row id, so paging through a sorted list never shows a row twice or skips one.
/// </para>
/// <para>
/// There is no scope here. The caller does not get to say whose rows it wants: that answer comes
/// from <c>IAccessGuard.RequireScope</c> and reaches the repository as a separate argument (AD-3).
/// </para>
/// <para>
/// The own-deliveries list takes the same query and refuses the two parts of it that are about a
/// counterparty - the driver and client filters and the two name sorts - because that list carries
/// no counterparty identity at all (AD-17), and ordering or narrowing by a name is a way of reading
/// one.
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListDeliveriesQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="DeliverySort"/> member's name, or null for newest first. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
/// <param name="Driver">Text the assigned driver's name must contain.</param>
/// <param name="Client">Text the requesting client's name must contain.</param>
/// <param name="Pickup">Text the pickup address must contain.</param>
/// <param name="Dropoff">Text the dropoff address must contain.</param>
/// <param name="PackageDetails">Text the package description must contain.</param>
/// <param name="DeliveryNotes">Text the delivery notes must contain.</param>
/// <param name="Status">The only status to list, or null for every status.</param>
/// <param name="CreatedFrom">The first local day a listed delivery may have been opened on.</param>
/// <param name="CreatedTo">The last local day a listed delivery may have been opened on, inclusive.</param>
/// <param name="OverdueOnly">True to list only deliveries whose window has passed (FR-19).</param>
public sealed record ListDeliveriesQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false,
    string? Driver = null,
    string? Client = null,
    string? Pickup = null,
    string? Dropoff = null,
    string? PackageDetails = null,
    string? DeliveryNotes = null,
    DeliveryStatus? Status = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    bool OverdueOnly = false);

/// <summary>The columns a delivery list can be ordered by (FR-20).</summary>
public enum DeliverySort
{
    /// <summary>
    /// When the parcel is first due: the near bound of its window, or the far bound when that is the
    /// only one. Deliveries promised no window sort to the far end in both directions.
    /// </summary>
    Window,

    /// <summary>The assigned driver's name. Not offered on the own-deliveries list.</summary>
    Driver,

    /// <summary>The requesting client's name. Not offered on the own-deliveries list.</summary>
    Client,

    /// <summary>The pickup address.</summary>
    Pickup,

    /// <summary>The dropoff address.</summary>
    Dropoff,

    /// <summary>What is being moved.</summary>
    PackageDetails,

    /// <summary>The free-text instructions.</summary>
    DeliveryNotes,

    /// <summary>Where the delivery is in its lifecycle, in lifecycle order rather than alphabetically.</summary>
    Status,

    /// <summary>When the delivery was opened.</summary>
    CreatedAt,
}
