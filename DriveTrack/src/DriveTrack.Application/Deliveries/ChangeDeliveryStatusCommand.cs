using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;
using FluentValidation;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// A request to advance a delivery's lifecycle.
/// <para>
/// The note rides on the same command rather than being a second call, because the note belongs on
/// the same entry: "delivery failed, nobody at the address" is one fact, and writing it as a status
/// change followed by a note would be two rows that a later reader has to join by their timestamps.
/// It is optional, so the ordinary advance carries none.
/// </para>
/// <para>
/// There is no delivery id here: the id is the route, as it is for every other single-row operation
/// in this capability.
/// </para>
/// </summary>
/// <param name="Status">
/// The status being asked for. Nullable because the wire can omit it, and a non-nullable property
/// would bind an absent field to the enum's first member — <see cref="DeliveryStatus.Pending"/> —
/// and turn "you forgot to say where" into a 409 about a transition the caller never requested.
/// </param>
/// <param name="Note">Free text explaining the change, or null.</param>
public sealed record ChangeDeliveryStatusCommand(DeliveryStatus? Status, string? Note);
