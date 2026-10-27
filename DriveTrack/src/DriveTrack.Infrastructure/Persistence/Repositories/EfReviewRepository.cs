using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Reviews;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IReviewRepository"/>. See <see cref="EfClientRepository"/> for the AD-6 rules every repository here follows.</summary>
internal sealed class EfReviewRepository(AppDbContext context) : IReviewRepository
{
    /// <inheritdoc />
    public Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.Reviews.FirstOrDefaultAsync(review => review.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Review>> ListAsync(
        AccessScope scope,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = context.Reviews.AsNoTracking();

        // AD-3: the scope is a WHERE, applied before OFFSET and LIMIT. Filtering the materialized
        // page instead would answer an empty page for a client whose reviews fall outside the first
        // hundred - the defect the whole scope parameter exists to make unrepresentable.
        if (scope.ClientId is { } clientId)
        {
            rows = rows.Where(review => review.ClientId == clientId);
        }

        // A driver's narrowing has to go through the delivery, because a review carries no driver
        // key. No shipped route reaches here with one - every review route refuses a driver - but
        // the scope is total over the roles that have one, and a repository that silently ignored
        // half of its own parameter would be a hole waiting for the first caller who needs it.
        if (scope.DriverId is { } driverId)
        {
            rows = rows.Where(review => context.Deliveries
                .Any(delivery => delivery.Id == review.DeliveryId && delivery.DriverId == driverId));
        }

        // Newest first, and ordered explicitly: PostgreSQL is free to return rows in any order
        // without an ORDER BY, and an unordered OFFSET is a page that can repeat and skip rows
        // between requests.
        //
        // Descending rather than ascending, because nobody pages past the first hundred. Ascending
        // put the newest rows on the last page, so a moderator of a product with more than a page
        // of reviews never reached the ones just written - a row nobody can reach is the defect. The
        // id is a total order on its own here, so no tie-break is needed.
        return await rows
            .OrderByDescending(review => review.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Paged<Review>> ListPageAsync(
        AccessScope scope,
        ListReviewsQuery query,
        ReviewSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var reviews = Scoped(context.Reviews.AsNoTracking(), scope);
        var descending = query.Descending;

        // Every order ends on the id. The two party sorts read each name off the Identity account
        // as a correlated subquery - the author through the client row, the driver through the
        // reviewed delivery - with the `(int)(object)` cast that is the one translatable spelling of
        // "the integer behind this typed id" (AD-22).
        return reviews.ToPageAsync(
            rows => sort switch
            {
                ReviewSort.Delivery => rows.SortBy(review => review.DeliveryId, descending)
                    .ThenSortBy(review => review.Id, descending),
                ReviewSort.Client => rows
                    .SortBy(
                        review => context.Clients
                            .Where(client => client.Id == review.ClientId)
                            .Join(
                                context.Users,
                                client => (int)(object)client.UserId,
                                user => user.Id,
                                (client, user) => (user.FirstName + " " + user.LastName).Trim())
                            .FirstOrDefault() ?? string.Empty,
                        descending)
                    .ThenSortBy(review => review.Id, descending),
                ReviewSort.Driver => rows
                    .SortBy(
                        review => context.Deliveries
                            .Where(delivery => delivery.Id == review.DeliveryId)
                            .Join(
                                context.Drivers,
                                delivery => delivery.DriverId,
                                driver => (DriverId?)driver.Id,
                                (delivery, driver) => driver)
                            .Join(
                                context.Users,
                                driver => (int)(object)driver.UserId,
                                user => user.Id,
                                (driver, user) => (user.FirstName + " " + user.LastName).Trim())
                            .FirstOrDefault() ?? string.Empty,
                        descending)
                    .ThenSortBy(review => review.Id, descending),
                ReviewSort.Rating => rows.SortBy(review => review.Rating, descending)
                    .ThenSortBy(review => review.Id, descending),
                ReviewSort.CreatedAt => rows.SortBy(review => review.CreatedAt, descending)
                    .ThenSortBy(review => review.Id, descending),

                // Newest first, for the reason ListAsync gives.
                _ => rows.SortBy(review => review.Id, !descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Review>> ListAllAsync(
        AccessScope scope,
        CancellationToken cancellationToken) =>
        await Scoped(context.Reviews.AsNoTracking(), scope)
            .OrderByDescending(review => review.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DriverRating>> ListDriverRatingsAsync(
        IReadOnlyCollection<DriverId> driverIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(driverIds);

        if (driverIds.Count == 0)
        {
            // No round trip for a question with no subject. A roster read on an empty fleet is the
            // ordinary case that reaches here, and `IN ()` is not a query worth issuing.
            return [];
        }

        // Reviews joined to their delivery's driver, grouped in the database. One round trip for
        // the whole roster; the alternative - a query per driver - is the shape that makes a forty
        // driver roster forty queries, and the alternative to that, pulling every review back and
        // averaging in memory, costs the whole table to answer a question about a page of it.
        var rows = await context.Reviews
            .AsNoTracking()
            .Join(
                context.Deliveries,
                review => review.DeliveryId,
                delivery => delivery.Id,
                (review, delivery) => new { delivery.DriverId, review.Rating })
            .Where(row => row.DriverId != null && driverIds.Contains(row.DriverId.Value))
            .GroupBy(row => row.DriverId!.Value)
            .Select(group => new
            {
                DriverId = group.Key,
                Average = group.Average(row => (double)row.Rating),
                ReviewCount = group.Count(),
            })
            .ToListAsync(cancellationToken);

        // A driver absent from `rows` has no rating, and stays absent: the caller renders the
        // roster's "no value" text for them rather than a zero.
        return [.. rows.Select(row => new DriverRating(row.DriverId, row.Average, row.ReviewCount))];
    }

    /// <inheritdoc />
    public void Add(Review review) => context.Reviews.Add(review);

    /// <inheritdoc />
    public void Remove(Review review) => context.Reviews.Remove(review);

    /// <summary>
    /// AD-3's narrowing as a WHERE. A client sees the reviews they wrote. A driver's narrowing has to
    /// go through the delivery, because a review carries no driver key; no shipped route reaches
    /// here with one, but a repository that silently ignored half its own parameter would be a hole
    /// waiting for the first caller who needs it.
    /// </summary>
    private IQueryable<Review> Scoped(IQueryable<Review> rows, AccessScope scope)
    {
        if (scope.ClientId is { } clientId)
        {
            rows = rows.Where(review => review.ClientId == clientId);
        }

        if (scope.DriverId is { } driverId)
        {
            rows = rows.Where(review => context.Deliveries
                .Any(delivery => delivery.Id == review.DeliveryId && delivery.DriverId == driverId));
        }

        return rows;
    }
}
