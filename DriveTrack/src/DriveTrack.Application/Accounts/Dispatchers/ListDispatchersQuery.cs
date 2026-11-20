namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// Paging over the dispatcher roster, with the roster screen's sort, applied in SQL over
/// every dispatcher before OFFSET and LIMIT.
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListDispatchersQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="DispatcherSort"/> member's name, or null for surname order. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
public sealed record ListDispatchersQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false);

/// <summary>The columns the dispatcher roster can be ordered by.</summary>
public enum DispatcherSort
{
    /// <summary>Given name.</summary>
    FirstName,

    /// <summary>Family name, then given name.</summary>
    LastName,

    /// <summary>Sign-in address.</summary>
    Email,
}
