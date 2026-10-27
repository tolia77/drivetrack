using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IDeliveryRepository"/>. See <see cref="EfClientRepository"/> for the AD-6 rules every repository here follows.</summary>
internal sealed class EfDeliveryRepository(AppDbContext context) : IDeliveryRepository
{
    /// <summary>
    /// The decimals a coordinate is shown with when a place has no address - the board's cell
    /// formats it <c>0.#####</c> - so the text the filter and the sort read is the text the cell
    /// shows, and typing what is on screen finds the row. Rounded as numeric and turned back into a
    /// double - <c>round(x::numeric, 5)::float8::text</c> - because PostgreSQL has no two-argument
    /// round for a double, and numeric's own text would keep the trailing zeros the cell drops.
    /// </summary>
    private const int CoordinateDecimals = 5;

    /// <inheritdoc />
    public Task<Delivery?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        // No Include, and the delete path depends on it: a tracked timeline entry would make EF
        // issue its own DELETE at trigger depth 1, where the append-only guard raises. The owned
        // locations still load, because an owned type is columns of this row rather than a
        // navigation to another one (AD-11).
        context.Deliveries.FirstOrDefaultAsync(delivery => delivery.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Delivery?> FindVisibleAsync(
        int id,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        var rows = context.Deliveries.AsNoTracking().Where(delivery => delivery.Id == id);

        // The same narrowing ListAsync applies, on a single row: a driver asking about a delivery
        // that is not theirs gets no row rather than a row they then have to be refused, so the
        // answer is 404 and nothing about the delivery's existence is disclosed.
        if (scope.DriverId is { } driverId)
        {
            rows = rows.Where(delivery => delivery.DriverId == driverId);
        }

        if (scope.ClientId is { } clientId)
        {
            rows = rows.Where(delivery => delivery.ClientId == clientId);
        }

        return rows.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Paged<DeliveryRow>> ListAsync(
        AccessScope scope,
        DeliveryListCriteria criteria,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // AD-3: the scope is the first WHERE, so every filter below narrows what the caller may see
        // and the total counts exactly that - never another client's rows, not even as a number.
        var deliveries = Scoped(context.Deliveries.AsNoTracking(), scope);

        // The filters on the delivery's own columns, before the names are joined on.
        if (criteria.Status is { } status)
        {
            deliveries = deliveries.Where(delivery => delivery.Status == status);
        }

        if (criteria.CreatedFrom is { } from)
        {
            var start = from.ToUniversalTime();

            deliveries = deliveries.Where(delivery => delivery.CreatedAt >= start);
        }

        if (criteria.CreatedBefore is { } before)
        {
            var end = before.ToUniversalTime();

            deliveries = deliveries.Where(delivery => delivery.CreatedAt < end);
        }

        if (criteria.OverdueAt is { } now)
        {
            // The shared rule as an expression, so the filter and the badge on each row are one
            // rule rather than two that agree today. Offset zero, because the column is timestamptz
            // and Npgsql only writes a DateTimeOffset parameter at offset zero.
            deliveries = deliveries.Where(OverdueRule.At(now.ToUniversalTime()));
        }

        if (ListQueries.Term(criteria.PackageDetails) is { } details)
        {
            var pattern = ListQueries.Contains(details);

            deliveries = deliveries.Where(delivery =>
                EF.Functions.ILike(delivery.PackageDetails, pattern, ListQueries.EscapeCharacter));
        }

        if (ListQueries.Term(criteria.DeliveryNotes) is { } notes)
        {
            var pattern = ListQueries.Contains(notes);

            deliveries = deliveries.Where(delivery =>
                EF.Functions.ILike(delivery.DeliveryNotes!, pattern, ListQueries.EscapeCharacter));
        }

        // The values the board shows that are not columns of this row: each party's name, read off
        // the Identity account two hops away, and each place as its cell reads it - the address, or
        // the coordinates when none has been resolved (FR-94). Correlated subqueries rather than
        // joins, so an unassigned delivery stays in the list with a null name. The `(int)(object)`
        // cast is the one spelling of "the integer behind this typed id" EF translates - a no-op
        // `::int` - because a value converter's `.Value` cannot be read inside a query (AD-22).
        var rows = deliveries.Select(delivery => new Row
        {
            Delivery = delivery,
            DriverName = context.Drivers
                .Where(driver => driver.Id == delivery.DriverId)
                .Join(
                    context.Users,
                    driver => (int)(object)driver.UserId,
                    user => user.Id,
                    (driver, user) => (user.FirstName + " " + user.LastName).Trim())
                .FirstOrDefault(),
            ClientName = context.Clients
                .Where(client => client.Id == delivery.ClientId)
                .Join(
                    context.Users,
                    client => (int)(object)client.UserId,
                    user => user.Id,
                    (client, user) => (user.FirstName + " " + user.LastName).Trim())
                .FirstOrDefault(),
            Pickup = delivery.PickupLocation.Address == null || delivery.PickupLocation.Address.Trim() == string.Empty
                ? ((double)Math.Round((decimal)delivery.PickupLocation.Latitude, CoordinateDecimals)).ToString()
                    + "; " + ((double)Math.Round((decimal)delivery.PickupLocation.Longitude, CoordinateDecimals)).ToString()
                : delivery.PickupLocation.Address,
            Dropoff = delivery.DropoffLocation.Address == null || delivery.DropoffLocation.Address.Trim() == string.Empty
                ? ((double)Math.Round((decimal)delivery.DropoffLocation.Latitude, CoordinateDecimals)).ToString()
                    + "; " + ((double)Math.Round((decimal)delivery.DropoffLocation.Longitude, CoordinateDecimals)).ToString()
                : delivery.DropoffLocation.Address,
            Due = delivery.WindowEarliestAt ?? delivery.WindowLatestAt,
        });

        if (ListQueries.Term(criteria.Driver) is { } driverName)
        {
            var pattern = ListQueries.Contains(driverName);

            rows = rows.Where(row => EF.Functions.ILike(row.DriverName!, pattern, ListQueries.EscapeCharacter));
        }

        if (ListQueries.Term(criteria.Client) is { } clientName)
        {
            var pattern = ListQueries.Contains(clientName);

            rows = rows.Where(row => EF.Functions.ILike(row.ClientName!, pattern, ListQueries.EscapeCharacter));
        }

        if (ListQueries.Term(criteria.Pickup) is { } pickup)
        {
            var pattern = ListQueries.Contains(pickup);

            rows = rows.Where(row => EF.Functions.ILike(row.Pickup, pattern, ListQueries.EscapeCharacter));
        }

        if (ListQueries.Term(criteria.Dropoff) is { } dropoff)
        {
            var pattern = ListQueries.Contains(dropoff);

            rows = rows.Where(row => EF.Functions.ILike(row.Dropoff, pattern, ListQueries.EscapeCharacter));
        }

        var descending = criteria.Descending;

        // Every order ends on the row id, in the same direction, so paging through a sorted list
        // never shows a row twice or skips one. A text column with no value - an unassigned driver,
        // an empty note - sorts as the empty string, at the start ascending and the end descending.
        var page = await rows.ToPageAsync(
            filtered => criteria.Sort switch
            {
                // By when the parcel is first due. Deliveries promised no window stay at the far end
                // in both directions: "nothing is due" is neither the earliest thing on the board
                // nor the latest.
                DeliverySort.Window => filtered.SortNullsLastBy(row => row.Due, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.Driver => filtered.SortBy(row => row.DriverName ?? string.Empty, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.Client => filtered.SortBy(row => row.ClientName ?? string.Empty, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.Pickup => filtered.SortBy(row => row.Pickup, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.Dropoff => filtered.SortBy(row => row.Dropoff, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.PackageDetails => filtered.SortBy(row => row.Delivery.PackageDetails, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.DeliveryNotes => filtered
                    .SortBy(row => row.Delivery.DeliveryNotes ?? string.Empty, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),

                // By the lifecycle rather than by the stored name: alphabetically "Delivered" would
                // come before "Pending" for no reason a dispatcher could name.
                DeliverySort.Status => filtered
                    .SortBy(ListQueries.Rank<Row, DeliveryStatus>(row => row.Delivery.Status), descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),
                DeliverySort.CreatedAt => filtered.SortBy(row => row.Delivery.CreatedAt, descending)
                    .ThenSortBy(row => row.Delivery.Id, descending),

                // Newest first. The id is a total order on its own, so it is the whole ORDER BY.
                _ => filtered.SortBy(row => row.Delivery.Id, !descending),
            },
            criteria.Offset,
            criteria.Limit,
            cancellationToken);

        return page.Map(row => new DeliveryRow(row.Delivery, row.DriverName, row.ClientName));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Delivery>> ListVisibleAsync(
        AccessScope scope,
        CancellationToken cancellationToken) =>
        await Scoped(context.Deliveries.AsNoTracking(), scope)
            .OrderByDescending(delivery => delivery.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Delivery>> ListByIdsAsync(
        AccessScope scope,
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            // An IN () clause is not a query PostgreSQL can run, and a round trip that can only
            // answer nothing is a round trip worth not making.
            return [];
        }

        // Distinct because the caller's rows may name one delivery more than once; the database
        // would answer the row once either way, but the parameter list has no reason to repeat.
        var keys = ids.Distinct().ToArray();

        var rows = context.Deliveries
            .AsNoTracking()
            .Where(delivery => keys.Contains(delivery.Id));

        // The same narrowing ListAsync applies (AD-3), and for the same reason: a row outside the
        // scope is absent from the answer rather than present and then filtered, so no caller can
        // learn of a delivery they may not see by naming its id.
        if (scope.DriverId is { } driverId)
        {
            rows = rows.Where(delivery => delivery.DriverId == driverId);
        }

        if (scope.ClientId is { } clientId)
        {
            rows = rows.Where(delivery => delivery.ClientId == clientId);
        }

        return await rows.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal?> FindHeaviestActiveWeightForDriverAsync(
        DriverId driverId,
        CancellationToken cancellationToken) =>
        // Max rather than Sum, and that is the whole reading of FR-103: a vehicle carries one
        // parcel at a time, so the question is whether the heaviest one fits, not whether the
        // driver's whole outstanding load would.
        //
        // One aggregate rather than a page of rows the caller would have to walk, and Max over a
        // nullable projection answers null for an empty set instead of throwing the way Max over a
        // non-nullable one would.
        context.Deliveries
            .AsNoTracking()
            .Where(delivery => delivery.DriverId == driverId)
            .Where(delivery => delivery.Status == DeliveryStatus.Pending
                || delivery.Status == DeliveryStatus.InTransit)
            .Select(delivery => (decimal?)delivery.PackageWeightKg)
            .MaxAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeliveryStatusCount>> CountByStatusAsync(
        CancellationToken cancellationToken) =>
        // Grouped in the database: the answer is four numbers, and the table is the whole history.
        await context.Deliveries
            .AsNoTracking()
            .GroupBy(delivery => delivery.Status)
            .Select(group => new DeliveryStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        // The shared rule as an expression, so the count and the board's per-row flag are one rule
        // rather than two that agree today. Offset zero, because the column is timestamptz and
        // Npgsql only writes a DateTimeOffset parameter at offset zero.
        context.Deliveries
            .AsNoTracking()
            .CountAsync(OverdueRule.At(now.ToUniversalTime()), cancellationToken);

    /// <inheritdoc />
    public Task<int> CountUnassignedOpenAsync(CancellationToken cancellationToken) =>
        context.Deliveries
            .AsNoTracking()
            .Where(delivery => delivery.DriverId == null)
            .Where(delivery => delivery.Status == DeliveryStatus.Pending
                || delivery.Status == DeliveryStatus.InTransit)
            .CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Delivery>> ListOpenStartingBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        int limit,
        CancellationToken cancellationToken)
    {
        var start = from.ToUniversalTime();
        var end = until.ToUniversalTime();

        // The id breaks a tie between two windows opening at the same minute, so the list does not
        // reshuffle between two reads of an unchanged table.
        return await context.Deliveries
            .AsNoTracking()
            .Where(delivery => delivery.Status == DeliveryStatus.Pending
                || delivery.Status == DeliveryStatus.InTransit)
            .Where(delivery => delivery.WindowEarliestAt >= start && delivery.WindowEarliestAt < end)
            .OrderBy(delivery => delivery.WindowEarliestAt)
            .ThenBy(delivery => delivery.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Add(Delivery delivery) => context.Deliveries.Add(delivery);

    /// <inheritdoc />
    public void Remove(Delivery delivery) => context.Deliveries.Remove(delivery);

    /// <summary>
    /// AD-3's narrowing as a WHERE: a driver sees the deliveries assigned to them, a client the ones
    /// they asked for, and dispatch every row.
    /// </summary>
    private static IQueryable<Delivery> Scoped(IQueryable<Delivery> rows, AccessScope scope)
    {
        if (scope.DriverId is { } driverId)
        {
            rows = rows.Where(delivery => delivery.DriverId == driverId);
        }

        if (scope.ClientId is { } clientId)
        {
            rows = rows.Where(delivery => delivery.ClientId == clientId);
        }

        return rows;
    }

    /// <summary>The shape the list query is filtered and sorted in: an initializer, so EF can see through it.</summary>
    private sealed class Row
    {
        public Delivery Delivery { get; init; } = null!;

        public string? DriverName { get; init; }

        public string? ClientName { get; init; }

        public string Pickup { get; init; } = string.Empty;

        public string Dropoff { get; init; } = string.Empty;

        public DateTimeOffset? Due { get; init; }
    }
}
