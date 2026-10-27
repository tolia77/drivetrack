using DriveTrack.Application.Common;
using DriveTrack.Application.Drivers;
using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions;

/// <summary>Persistence for <see cref="Driver"/>. See <see cref="IClientRepository"/> for the shape rules (AD-6).</summary>
public interface IDriverRepository
{
    /// <summary>
    /// Loads a driver with the vehicle they hold, or null when there is none with that id.
    /// </summary>
    /// <remarks>
    /// The vehicle travels with the driver so the summary can name it without the Drivers
    /// capability reaching into <c>unitOfWork.Vehicles</c>, which AD-24 reserves for the one writer
    /// of <c>drivers.vehicle_id</c>.
    /// </remarks>
    Task<Driver?> GetByIdAsync(DriverId id, CancellationToken cancellationToken);

    /// <summary>
    /// Every driver, with the vehicle each holds (FR-35). Unpaged, for the readers that need the
    /// whole roster rather than a table's page: the pickers, the chat roster and the dashboard.
    /// </summary>
    Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One page of the roster with each driver's name and address, searched and sorted in SQL over
    /// every driver, and the number the search matched (FR-35, FR-36, NFR-27). Oldest first when no
    /// column is named; every order ends on the id.
    /// <para>
    /// The name and the address live on the Identity account, so they are joined here rather than
    /// read per driver afterwards: the search and three of the sorts are about them. The rating
    /// sort reads the reviews of each driver's deliveries the same way, as an aggregate in the
    /// ORDER BY; the rating a row <em>shows</em> is still the Reviews capability's answer (AD-24).
    /// </para>
    /// </summary>
    /// <param name="query">The page, the search and the direction. Already validated.</param>
    /// <param name="sort">The column, already parsed; null for the roster's own order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<DriverRow>> ListPageAsync(
        ListDriversQuery query,
        DriverSort? sort,
        CancellationToken cancellationToken);

    /// <summary>Stages a new driver for the next commit.</summary>
    void Add(Driver driver);

    /// <summary>Stages a driver for deletion (FR-39).</summary>
    void Remove(Driver driver);
}

/// <summary>A driver, with the vehicle they hold, and the account fields a roster row shows.</summary>
/// <param name="Driver">The driver row, with its vehicle loaded.</param>
/// <param name="FirstName">The account's given name.</param>
/// <param name="LastName">The account's family name.</param>
/// <param name="Email">The account's sign-in address.</param>
public sealed record DriverRow(Driver Driver, string FirstName, string LastName, string Email);
