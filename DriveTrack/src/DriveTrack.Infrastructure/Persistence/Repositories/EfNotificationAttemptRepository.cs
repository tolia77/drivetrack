using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Common;
using DriveTrack.Application.Notifications;
using DriveTrack.Domain.Deliveries;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="INotificationAttemptRepository"/>. See <see cref="EfClientRepository"/> for the AD-6 rules every repository here follows.</summary>
internal sealed class EfNotificationAttemptRepository(AppDbContext context) : INotificationAttemptRepository
{
    /// <inheritdoc />
    public Task<NotificationAttempt?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.NotificationAttempts.FirstOrDefaultAsync(attempt => attempt.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Paged<NotificationAttempt>> ListAsync(
        ListNotificationAttemptsQuery query,
        NotificationSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var descending = query.Descending;

        // AD-3: ordered before OFFSET and LIMIT, because an unordered OFFSET is a page that can
        // repeat and skip rows between requests - and every order ends on the id, because two
        // attempts written by one commit share an instant to the microsecond.
        //
        // With no column named the log reads newest first; the direction flag turns that round.
        return context.NotificationAttempts.AsNoTracking().ToPageAsync(
            rows => sort switch
            {
                NotificationSort.Delivery => rows.SortBy(attempt => attempt.DeliveryId, descending)
                    .ThenSortBy(attempt => attempt.Id, descending),
                NotificationSort.Kind => rows
                    .SortBy(ListQueries.Rank<NotificationAttempt, NotificationKind>(attempt => attempt.Kind), descending)
                    .ThenSortBy(attempt => attempt.Id, descending),
                NotificationSort.Recipient => rows.SortBy(attempt => attempt.Recipient, descending)
                    .ThenSortBy(attempt => attempt.Id, descending),
                NotificationSort.Outcome => rows
                    .SortBy(ListQueries.Rank<NotificationAttempt, NotificationOutcome>(attempt => attempt.Outcome), descending)
                    .ThenSortBy(attempt => attempt.Id, descending),
                NotificationSort.AttemptedAt => rows.SortBy(attempt => attempt.AttemptedAt, descending)
                    .ThenSortBy(attempt => attempt.Id, descending),
                _ => rows.SortBy(attempt => attempt.AttemptedAt, !descending)
                    .ThenSortBy(attempt => attempt.Id, !descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);
    }

    /// <inheritdoc />
    public void Add(NotificationAttempt attempt) => context.NotificationAttempts.Add(attempt);
}
