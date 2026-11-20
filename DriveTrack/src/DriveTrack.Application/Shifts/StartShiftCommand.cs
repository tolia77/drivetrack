using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// What a driver sends to go on duty.
/// <para>
/// The instant is not a field and never can be: it is <c>TimeProvider.GetUtcNow()</c>'s answer
/// inside the service. A start a caller could name is a start a caller could backdate, and
/// the whole point of a shift is that it records when somebody actually went on duty.
/// </para>
/// <para>
/// The driver <em>is</em> a field, because dispatch may put a driver on duty as well as the driver
/// themselves. It is not a hole: <c>IAccessGuard.RequireShiftOwner</c> refuses a driver
/// who names anybody but themselves.
/// </para>
/// </summary>
/// <param name="DriverId">The driver going on duty.</param>
public sealed record StartShiftCommand(DriverId DriverId);
