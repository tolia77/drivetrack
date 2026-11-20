using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// What a caller sends to record a shift that already happened: dispatch for anybody's driver, a
/// driver for their own.
/// <para>
/// The same rule the corrections carry, and the same guard member states it —
/// <c>IAccessGuard.RequireShiftOwner</c> passes dispatch for every driver and a driver only for
/// themselves. A driver may correct their own shifts, and a shift they forgot to log at
/// all is the first correction they need; withholding this one route while leaving them the edit and
/// the delete would be a rule nothing else in the capability keeps.
/// </para>
/// <para>
/// Both instants are the caller's here, unlike <see cref="StartShiftCommand"/>: this is the
/// after-the-fact path, and somebody entering a shift that has already been worked is naming a
/// window rather than reporting the present moment.
/// </para>
/// </summary>
/// <param name="DriverId">The driver the shift belongs to.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null to open it.</param>
public sealed record CreateShiftCommand(
    DriverId DriverId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
