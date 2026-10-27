namespace DriveTrack.Application.Notifications;

/// <summary>
/// NFR-27's paging over the notification log, expressed as a request rather than as two loose
/// integers — the same shape <c>ListDeliveriesQuery</c> takes, because the rule is the same rule.
/// <para>
/// The log is the list that most needed one: the table grows by a row per status change per
/// delivery and nothing prunes it, so a read with no bound materialises the whole history every
/// time an administrator opens the screen.
/// </para>
/// <para>
/// The sort is applied in SQL over the whole log, before OFFSET and LIMIT, so the oldest attempt is
/// reachable by paging to the end or by turning the order round, and the page carries the log's
/// total so the screen can say how far through it the reader is.
/// </para>
/// <para>
/// Nor is there a scope. The caller does not get to say whose rows it wants: the log is an
/// administrator's whole view, decided by <c>IAccessGuard.RequireRole</c> inside the service (AD-2).
/// </para>
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListNotificationAttemptsQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="NotificationSort"/> member's name, or null for newest first. Any other text is
/// refused with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
public sealed record ListNotificationAttemptsQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false);

/// <summary>The columns the notification log can be ordered by.</summary>
public enum NotificationSort
{
    /// <summary>The delivery the attempt was about, by id.</summary>
    Delivery,

    /// <summary>Which notice it was, in the order the enum declares them.</summary>
    Kind,

    /// <summary>The address it went to.</summary>
    Recipient,

    /// <summary>How it went, in the order the enum declares the outcomes.</summary>
    Outcome,

    /// <summary>When it was made.</summary>
    AttemptedAt,
}
