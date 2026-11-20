using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Drivers;

/// <summary>
/// The driver capability. It owns <c>Driver</c> and reaches the driver–vehicle
/// assignment only through the one writer the Vehicles capability owns.
/// <para>
/// Every method takes an authorization decision through <see cref="Authorization.IAccessGuard"/>,
/// inline in its own body: nothing here is in <see cref="Authorization.PublicEntryPoints"/>.
/// </para>
/// <para>
/// A <see cref="DriverSummary"/> also carries the driver's rating. It is read through
/// <c>IReviewService</c> rather than computed here, because it is derived from reviews and reviews
/// belong to another capability — and it is derived on every read rather than stored on the
/// driver row, so a review written, edited or deleted a moment ago is already reflected.
/// A driver nobody has reviewed carries <c>null</c>, never a zero.
/// </para>
/// <para>
/// It also carries the on-duty flag, read through <c>IShiftService</c> for the
/// same reason and on the same terms: it is derived from the driver's open shift, shifts belong to
/// another capability, and nothing here reads <c>unitOfWork.Shifts</c>. The flag marks an off-duty
/// driver on the assignment form and never withholds one.
/// </para>
/// </summary>
public interface IDriverService
{
    /// <summary>
    /// One page of the roster, searched and sorted over every driver, with the number the
    /// search matched. Dispatcher or admin.
    /// </summary>
    /// <exception cref="Common.ForbiddenException">The caller runs neither dispatch nor the system.</exception>
    /// <exception cref="Common.ValidationException">
    /// The paging parameters are out of range, or the sort names no column.
    /// </exception>
    Task<Common.Paged<DriverSummary>> ListAsync(
        ListDriversQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every driver, each with the vehicle they hold, unpaged. For the pickers that have to
    /// offer every driver - the delivery form's assignment, the shift form's driver - and for the
    /// chat roster; a table's page must not cap any of them.
    /// </summary>
    Task<IReadOnlyList<DriverSummary>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>One driver.</summary>
    /// <exception cref="Common.NotFoundException">No driver has that id.</exception>
    Task<DriverSummary> GetAsync(DriverId id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes on a driver: the account, the driver row and the assignment, in one transaction.
    /// </summary>
    /// <exception cref="Common.ValidationException">A field is missing or too long.</exception>
    /// <exception cref="Common.NotFoundException">No vehicle has the named id.</exception>
    /// <exception cref="Common.ConflictException">
    /// The email is already in use, or another driver already holds the named vehicle.
    /// Either way nothing is written — an account created beside a refused assignment is exactly the
    /// half-written state the unit of work exists to prevent.
    /// </exception>
    Task<DriverSummary> CreateAsync(CreateDriverCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Changes a driver's name, licence number, assignment, or any of them. A rename
    /// is written to the account, and only when the name actually changed.
    /// </summary>
    /// <exception cref="Common.NotFoundException">No driver has that id, or no vehicle has the named id.</exception>
    /// <exception cref="Common.ValidationException">The merged state is one the driver may not hold.</exception>
    /// <exception cref="Common.ConflictException">Another driver already holds the named vehicle.</exception>
    /// <exception cref="Common.DomainRuleException">
    /// The new vehicle cannot carry a delivery the driver already holds.
    /// </exception>
    Task<DriverSummary> UpdateAsync(
        DriverId id,
        UpdateDriverCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a driver and the account behind them.
    /// <para>
    /// Never an error for a driver holding active deliveries: they are left
    /// unassigned, which the declared <c>Driver → Delivery</c> SetNull already guarantees. The
    /// original answered a raw database error here.
    /// </para>
    /// </summary>
    /// <exception cref="Common.NotFoundException">No driver has that id.</exception>
    Task DeleteAsync(DriverId id, CancellationToken cancellationToken);
}
