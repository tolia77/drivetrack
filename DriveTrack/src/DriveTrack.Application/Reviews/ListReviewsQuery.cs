using DriveTrack.Application.Deliveries;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// Paging over a review list, expressed as a request rather than as two loose integers —
/// the same shape <see cref="ListDeliveriesQuery"/> takes, because the rule is the same rule. The
/// sort is applied in SQL over every review the caller may see, before OFFSET and LIMIT.
/// <para>
/// There is no scope here. The caller does not get to say whose rows it wants: that answer comes
/// from the guard and reaches the repository as a separate argument.
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListReviewsQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="ReviewSort"/> member's name, or null for newest first. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>; the author's own list also refuses the two party columns, which
/// it does not carry.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
public sealed record ListReviewsQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false);

/// <summary>The columns a review list can be ordered by.</summary>
public enum ReviewSort
{
    /// <summary>The reviewed delivery, by id.</summary>
    Delivery,

    /// <summary>The author's name. Moderation list only.</summary>
    Client,

    /// <summary>The name of the driver who carried the delivery. Moderation list only.</summary>
    Driver,

    /// <summary>The score.</summary>
    Rating,

    /// <summary>When it was written.</summary>
    CreatedAt,
}
