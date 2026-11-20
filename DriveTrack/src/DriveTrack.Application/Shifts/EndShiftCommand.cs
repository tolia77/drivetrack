using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Shifts;

/// <summary>What a driver sends to go off duty. The instant is the injected clock's, as it is on the way in.</summary>
/// <param name="DriverId">The driver going off duty.</param>
public sealed record EndShiftCommand(DriverId DriverId);
