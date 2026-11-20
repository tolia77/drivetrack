using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;
using FluentValidation;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// A standalone note on a delivery's timeline, changing nothing else.
/// <para>
/// The entry it produces carries neither status, which is the shape a standalone note takes and the
/// shape a correction is made of: the timeline is append-only, so the way to correct a line is
/// to write another one.
/// </para>
/// </summary>
/// <param name="Note">
/// The text. Nullable on the type because the wire may send anything and the command has to carry
/// whatever arrived as far as the validator, which refuses it by name.
/// </param>
public sealed record AddDeliveryNoteCommand(string? Note);
