using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Common;
using DriveTrack.Application.Drivers;
using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IDriverRepository"/>. See <see cref="EfClientRepository"/> for the AD-6 rules every repository here follows.</summary>
internal sealed class EfDriverRepository(AppDbContext context) : IDriverRepository
{
    /// <inheritdoc />
    public Task<Driver?> GetByIdAsync(DriverId id, CancellationToken cancellationToken) =>
        // Tracked, because this is the row an update writes through, and the vehicle travels with
        // it so a summary can name the assignment without a second capability's repository.
        context.Drivers
            .Include(driver => driver.Vehicle)
            .FirstOrDefaultAsync(driver => driver.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken) =>
        await context.Drivers
            .AsNoTracking()
            .Include(driver => driver.Vehicle)
            .OrderBy(driver => driver.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Paged<DriverRow>> ListPageAsync(
        ListDriversQuery query,
        DriverSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The account fields are joined on rather than read per driver afterwards: the search and
        // three of the sorts are about them. An inner join, because a driver row without its account
        // breaks a foreign key the schema declares; `(int)(object)` is the one translatable spelling
        // of "the integer behind this typed id" (AD-22).
        var rows = context.Drivers
            .AsNoTracking()
            .Join(
                context.Users,
                driver => (int)(object)driver.UserId,
                user => user.Id,
                (driver, user) => new Row
                {
                    Driver = driver,
                    Vehicle = driver.Vehicle,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email ?? string.Empty,
                });

        if (ListQueries.Term(query.Search) is { } term)
        {
            var pattern = ListQueries.Contains(term);

            rows = rows.Where(row =>
                EF.Functions.ILike(row.FirstName, pattern, ListQueries.EscapeCharacter)
                || EF.Functions.ILike(row.LastName, pattern, ListQueries.EscapeCharacter)
                || EF.Functions.ILike(row.Email, pattern, ListQueries.EscapeCharacter)
                || EF.Functions.ILike(row.Driver.LicenseNumber, pattern, ListQueries.EscapeCharacter)
                || (row.Vehicle != null
                    && (EF.Functions.ILike(row.Vehicle.Model, pattern, ListQueries.EscapeCharacter)
                        || EF.Functions.ILike(row.Vehicle.LicensePlate, pattern, ListQueries.EscapeCharacter))));
        }

        var descending = query.Descending;

        var page = await rows.ToPageAsync(
            filtered => sort switch
            {
                DriverSort.FirstName => filtered.SortBy(row => row.FirstName, descending)
                    .ThenSortBy(row => row.Driver.Id, descending),
                DriverSort.LastName => filtered.SortBy(row => row.LastName, descending)
                    .ThenSortBy(row => row.FirstName, descending)
                    .ThenSortBy(row => row.Driver.Id, descending),
                DriverSort.Email => filtered.SortBy(row => row.Email, descending)
                    .ThenSortBy(row => row.Driver.Id, descending),
                DriverSort.LicenseNumber => filtered.SortBy(row => row.Driver.LicenseNumber, descending)
                    .ThenSortBy(row => row.Driver.Id, descending),

                // A driver with no vehicle is neither first nor last in a list of vehicles.
                DriverSort.Vehicle => filtered
                    .SortNullsLastBy(row => row.Vehicle == null ? null : row.Vehicle.Model, descending)
                    .ThenSortBy(row => row.Driver.Id, descending),

                // FR-98's average, as an aggregate over the reviews of the driver's deliveries - the
                // same join ListDriverRatingsAsync groups. Unreviewed drivers have none, and stay at
                // the far end in both directions.
                DriverSort.Rating => filtered
                    .SortNullsLastBy(
                        row => context.Reviews
                            .Join(
                                context.Deliveries,
                                review => review.DeliveryId,
                                delivery => delivery.Id,
                                (review, delivery) => new { delivery.DriverId, review.Rating })
                            .Where(rated => rated.DriverId == row.Driver.Id)
                            .Average(rated => (double?)rated.Rating),
                        descending)
                    .ThenSortBy(row => row.Driver.Id, descending),
                _ => filtered.SortBy(row => row.Driver.Id, descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);

        return page.Map(row =>
        {
            // The vehicle travels with the driver, exactly as ListAsync's Include loads it, so the
            // summary can name the assignment without a second capability's repository.
            row.Driver.Vehicle = row.Vehicle;

            return new DriverRow(row.Driver, row.FirstName, row.LastName, row.Email);
        });
    }

    /// <inheritdoc />
    public void Add(Driver driver) => context.Drivers.Add(driver);

    /// <inheritdoc />
    public void Remove(Driver driver) => context.Drivers.Remove(driver);

    /// <summary>The shape the roster query is filtered and sorted in: an initializer, so EF can see through it.</summary>
    private sealed class Row
    {
        public Driver Driver { get; init; } = null!;

        public Vehicle? Vehicle { get; init; }

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;
    }
}
