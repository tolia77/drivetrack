using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// A shift as a screen reads one: the row, the driver it belongs to by name, and
/// whether it is still running.
/// </summary>
/// <param name="Id">The shift row's id.</param>
/// <param name="DriverId">The driver on duty.</param>
/// <param name="DriverName">
/// That driver's name, read from the Identity account rather than joined — a driver's name lives on
/// their account and nowhere else, which is how the Shifts capability names a driver without
/// depending on the Drivers one.
/// </param>
/// <param name="StartedAt">When the shift started, at offset zero.</param>
/// <param name="EndedAt">When it ended, or null while it is open.</param>
/// <param name="IsOpen">
/// Whether the driver is still on duty. Derived from <paramref name="EndedAt"/> rather than stored:
/// there is no status column, and a second answer is a second thing that can disagree.
/// </param>
public sealed record ShiftSummary(
    int Id,
    DriverId DriverId,
    string DriverName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    bool IsOpen);
