using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Vehicles;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Vehicles;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for <see cref="Vehicle"/>. See <see cref="IClientRepository"/> for the shape rules.
/// <para>
/// The single-owner rule gives the driver–vehicle assignment to the Vehicles capability, so the two
/// questions about <c>drivers.vehicle_id</c> — which vehicles nobody holds, and who holds this one
/// — are asked here rather than on <see cref="IDriverRepository"/>, even though the column lives on
/// the driver row.
/// </para>
/// </summary>
public interface IVehicleRepository
{
    /// <summary>Loads a vehicle, or null when there is none with that id.</summary>
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the fleet, searched and sorted in SQL over every vehicle, with the number of
    /// vehicles the search matched. Oldest first when no sort is named; every order
    /// ends on the id, so OFFSET never repeats or skips a row.
    /// </summary>
    /// <param name="query">The page, the search and the direction. The caller has already validated it.</param>
    /// <param name="sort">The column, already parsed; null for the fleet's own order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<Vehicle>> ListAsync(
        ListVehiclesQuery query,
        VehicleSort? sort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every vehicle, oldest first. Unpaged on purpose: it feeds the delivery form's capacity hint,
    /// which has to know every vehicle a driver might hold rather than the ones on a table's page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Vehicle>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every vehicle no driver holds. Unpaged, exactly as the original's
    /// <c>GET /vehicles/unassigned</c> was: it feeds an assignment control, which needs the whole
    /// set to be a correct choice.
    /// </summary>
    Task<IReadOnlyList<Vehicle>> ListUnassignedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads the vehicle carrying that licence plate, or null when no vehicle does. The
    /// plate is compared as stored, so the caller passes the normalized form — the same one it
    /// would write — and the unique index on the column answers the same question.
    /// <para>
    /// The friendly half of the uniqueness rule, mirroring
    /// <see cref="IUserAccountRepository.EmailExistsAsync"/>: the unique index on the column is the
    /// race backstop and answers the same 409 through <c>PERSISTENCE_UNIQUE_VIOLATION</c>.
    /// It returns the row rather than a boolean because an update has to be able to tell "another
    /// vehicle holds this plate" from "this vehicle already holds it".
    /// </para>
    /// </summary>
    Task<Vehicle?> FindByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken);

    /// <summary>
    /// The driver currently holding that vehicle, or null when none does.
    /// <para>
    /// One method for two rules: a held vehicle cannot be deleted, and one another driver already
    /// holds cannot be assigned. Both ask the same question, so both ask it here.
    /// </para>
    /// </summary>
    Task<DriverId?> FindHolderAsync(int vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// The first <paramref name="limit"/> vehicles by next maintenance date, soonest first, with
    /// the vehicles that have no date after every one that has.
    /// </summary>
    Task<IReadOnlyList<Vehicle>> ListByNextMaintenanceAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Stages a new vehicle for the next commit.</summary>
    void Add(Vehicle vehicle);

    /// <summary>Stages a vehicle for deletion.</summary>
    void Remove(Vehicle vehicle);
}
