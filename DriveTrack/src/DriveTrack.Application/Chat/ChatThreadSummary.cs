
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Application.Chat;

/// <summary>
/// One row of the dispatch desk's roster of conversations: enough to name a thread, to tell
/// one driver from another at a glance, and to open it. Still deliberately no message count, no
/// preview and no unread count.
/// <para>
/// The three facts beside the name cost nothing. <c>ListThreadsAsync</c> already reads the roster
/// through <c>IDriverService.ListAllAsync</c> and is handed a <c>DriverSummary</c> carrying the
/// vehicle, its plate and whether the driver is on duty; carrying only the name meant discarding
/// them and leaving a dispatcher to tell two people apart by a name they may share. No extra query,
/// no extra round trip — the same rows, read rather than thrown away.
/// </para>
/// </summary>
/// <param name="DriverId">The driver row the thread is keyed on.</param>
/// <param name="DriverName">The driver's display name, which the roster is ordered by.</param>
/// <param name="VehicleModel">
/// The vehicle they are assigned, or null when they have none. Null rather than an empty string:
/// "no vehicle assigned" is a fact about the driver, and a blank line in the roster is not one.
/// </param>
/// <param name="VehicleLicensePlate">That vehicle's plate, or null for the same reason.</param>
/// <param name="OnDuty">Whether a shift of theirs is open right now.</param>
public sealed record ChatThreadSummary(
    DriverId DriverId,
    string DriverName,
    string? VehicleModel,
    string? VehicleLicensePlate,
    bool OnDuty);
