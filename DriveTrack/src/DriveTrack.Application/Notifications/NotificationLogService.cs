using DriveTrack.Application.Abstractions.Persistence;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Notifications;
using FluentValidation;

namespace DriveTrack.Application.Notifications;

/// <summary>
/// The operation pipeline over the notification log, which on a read-only capability is guard →
/// validate → read → map.
/// <para>
/// The guard is called through the interface, inline in this method's own body. Folding it
/// into a helper would read no better and would not count — the coverage gate walks the method's IL
/// and does not follow a call it makes.
/// </para>
/// <para>
/// <c>RequireRole(UserRole.Admin)</c> rather than <c>Dispatcher</c>, and the difference matters
/// here: an administrator satisfies every check by rule inside the guard, so naming
/// <c>Dispatcher</c> would admit both. The log is an operations record — it carries the address of
/// every client the system has written to — and it is addressed to the administrator.
/// </para>
/// </summary>
public sealed class NotificationLogService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IAccessGuard accessGuard,
    IValidator<ListNotificationAttemptsQuery> listValidator) : INotificationLogService
{
    /// <inheritdoc />
    public async Task<Paged<NotificationAttemptSummary>> ListAsync(
        ListNotificationAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        accessGuard.RequireRole(UserRole.Admin);

        // Bad paging is refused before any query is built. After the guard, so a caller who may not
        // read the log is told that rather than told their paging is wrong.
        await listValidator.EnsureValidAsync(query, cancellationToken);

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var attempts = await unitOfWork.NotificationAttempts.ListAsync(
            query,
            ListSort.Parse<NotificationSort>(query.Sort),
            cancellationToken);

        // Nothing was written, so the scope is disposed without a commit and the empty transaction
        // rolls back. That is the ordinary read path, not an omission.
        return attempts.Map(Summary);
    }

    private static NotificationAttemptSummary Summary(NotificationAttempt attempt) =>
        new(
            attempt.Id,
            attempt.DeliveryId,
            attempt.Kind,
            attempt.Recipient,
            attempt.AttemptedAt,
            attempt.Outcome,
            attempt.Error);
}
