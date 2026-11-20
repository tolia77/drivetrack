using DriveTrack.Application.Common;
using DriveTrack.Application.Shifts;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Shifts;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>A shift and the name of the driver it belongs to, as one list row.</summary>
/// <param name="Shift">The shift.</param>
/// <param name="DriverName">The driver's first and last name, or empty when the account is missing.</param>
public sealed record ShiftRow(Shift Shift, string DriverName);
