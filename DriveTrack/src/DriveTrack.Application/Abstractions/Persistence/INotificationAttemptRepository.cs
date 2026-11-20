using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Notifications;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Notifications;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for <see cref="NotificationAttempt"/>. No <c>Remove</c>: an attempt is a record
/// of what happened, removed only by the cascade from its delivery.
/// </summary>
public interface INotificationAttemptRepository
{
    /// <summary>
    /// One page of attempts, newest first.
    /// <para>
    /// The remediation for a silently swallowed notification is "a row an administrator can read",
    /// and a row nothing can read is a log line with a table behind it. Paged like every other list
    /// that pages, and this is the table that most needed it: it grows by a row per status change
    /// per delivery and nothing prunes it, so an unbounded read is the whole history materialised
    /// every time the screen is opened.
    /// </para>
    /// <para>
    /// Sorted in SQL over the whole log, with the log's total, so the oldest attempt is reachable
    /// from the screen and the pager can say how far through the history a page is.
    /// </para>
    /// </summary>
    /// <param name="query">The page and the direction. The caller has already validated it.</param>
    /// <param name="sort">The column, already parsed; null for newest first.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<NotificationAttempt>> ListAsync(
        ListNotificationAttemptsQuery query,
        NotificationSort? sort,
        CancellationToken cancellationToken);

    /// <summary>Stages a new attempt for the next commit.</summary>
    void Add(NotificationAttempt attempt);
}
