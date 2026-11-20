
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Application.Dashboard;

/// <summary>One driver on the roster card.</summary>
/// <param name="Id">The driver's id.</param>
/// <param name="Name">The driver's display name.</param>
/// <param name="VehicleModel">The assigned vehicle's model, or null with no vehicle.</param>
/// <param name="VehicleLicensePlate">The assigned vehicle's plate, or null with no vehicle.</param>
/// <param name="Rating">The average review score, or null when nobody has reviewed them.</param>
/// <param name="ReviewCount">How many reviews the average is over.</param>
/// <param name="OnDuty">Whether the driver has an open shift.</param>
public sealed record DashboardDriver(
    DriverId Id,
    string Name,
    string? VehicleModel,
    string? VehicleLicensePlate,
    double? Rating,
    int ReviewCount,
    bool OnDuty);
