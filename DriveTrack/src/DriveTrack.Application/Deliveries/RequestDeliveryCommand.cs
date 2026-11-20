using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Locations;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// What a client sends to ask for a delivery.
/// <para>
/// A separate type from <see cref="CreateDeliveryCommand"/> rather than the same one with fields a
/// client is told not to fill in. A client sets neither the driver nor the status, and
/// the way to make that true is to leave nowhere to write them: there is no <c>DriverId</c>, no
/// <c>ClientId</c> and no <c>Status</c> on this record, so the wire cannot carry one and no future
/// edit to the request path can quietly start honouring one.
/// </para>
/// <para>
/// The client row is the guard's answer rather than a field, for the same reason. A
/// <c>ClientId</c> here would be a number a caller could change, and "the delivery is attached to
/// whoever asked for it" would become a rule a service had to remember to enforce instead of a
/// shape it could not violate.
/// </para>
/// <para>
/// No delivery window either, and that is deliberate rather than an omission: a window is
/// dispatch's commitment about when a parcel arrives, not the requester's about when it may. A
/// client who needs one says so in the notes, and dispatch sets the bounds through the edit path.
/// </para>
/// </summary>
/// <param name="Pickup">Where the parcel is collected. Required.</param>
/// <param name="Dropoff">Where the parcel is delivered. Required.</param>
/// <param name="PackageDetails">What is being moved. Required.</param>
/// <param name="PackageWeightKg">Weight in kilograms; must be positive.</param>
/// <param name="DeliveryNotes">Free-text instructions, or null.</param>
public sealed record RequestDeliveryCommand(
    LocationInput? Pickup,
    LocationInput? Dropoff,
    string? PackageDetails,
    decimal PackageWeightKg,
    string? DeliveryNotes);
