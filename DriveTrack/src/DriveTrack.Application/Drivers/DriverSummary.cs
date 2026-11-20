using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Drivers;

/// <summary>
/// A driver as a dispatcher sees them: the account's identity fields, the licence number
/// the driver row carries, and the vehicle they hold. A DTO, so <c>Driver</c>, <c>Vehicle</c> and
/// the EF navigation between them never bind to a component parameter or serialize.
/// <para>
/// <see cref="Id"/> and <see cref="UserId"/> are both here and are deliberately different types:
/// the driver row's key addresses this capability's rows, the user id addresses the account, and
/// the identity types are distinct so the two cannot be confused into authorizing the wrong caller.
/// </para>
/// <para>
/// The vehicle is flattened into three fields rather than nested. It is what the list and the
/// browser-side search read, and the search matches text — so the model and the plate travel as text.
/// </para>
/// </summary>
/// <param name="Id">The driver row's id.</param>
/// <param name="UserId">The account this driver is.</param>
/// <param name="FirstName">Given name, from the account.</param>
/// <param name="LastName">Family name, from the account.</param>
/// <param name="Email">The address the account signs in with.</param>
/// <param name="LicenseNumber">Driving licence number as recorded by a dispatcher.</param>
/// <param name="VehicleId">The vehicle held, or null when the driver holds none.</param>
/// <param name="VehicleModel">Its make and model, or null when the driver holds none.</param>
/// <param name="VehicleLicensePlate">Its registration plate, or null when the driver holds none.</param>
/// <param name="Rating">
/// The mean of every rating clients have given this driver's deliveries, or <c>null</c> when
/// nobody has reviewed one. Derived on demand through <c>IReviewService</c> and stored nowhere
/// (reviews own the rating): a column on <c>drivers</c> would be a second answer that goes stale the moment a
/// review is written, edited or deleted.
/// <para>
/// Null rather than zero, and the distinction is the whole of this field's contract. A zero is a
/// rating — the worst one the scale can express — so mapping "nobody has said anything" onto it
/// would sort an unrated driver below every rated one and put a complaint nobody made in front of a
/// dispatcher. The screen renders its "no value" text for null and never a number.
/// </para>
/// </param>
/// <param name="ReviewCount">
/// How many reviews the mean was drawn from. Zero when <see cref="Rating"/> is null, and the two
/// always agree: an average of nothing is not a number.
/// </param>
/// <param name="OnDuty">
/// Whether this driver has an open shift right now. Derived on demand through
/// <c>IShiftService</c> and stored nowhere (shifts own it): a column on <c>drivers</c> would be a second
/// answer that goes stale the moment somebody goes on or off duty, and the one answer the system has
/// is <c>shifts.ended_at IS NULL</c> — which is also the filter of the index enforcing it.
/// <para>
/// A flag, never a gate. The assignment form is to <em>mark</em> an off-duty driver, not to
/// withhold them: dispatch routinely assigns a parcel to a driver who starts their shift in an hour,
/// and a picker that hid them would turn a note into a refusal nobody asked for.
/// </para>
/// </param>
public sealed record DriverSummary(
    DriverId Id,
    UserId UserId,
    string FirstName,
    string LastName,
    string Email,
    string LicenseNumber,
    int? VehicleId,
    string? VehicleModel,
    string? VehicleLicensePlate,
    double? Rating,
    int ReviewCount,
    bool OnDuty);
