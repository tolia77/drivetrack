using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Locations;
using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Dashboard;

/// <summary>One of today's deliveries.</summary>
/// <param name="Id">The delivery's id.</param>
/// <param name="WindowEarliestAt">The window's opening bound.</param>
/// <param name="WindowLatestAt">The window's closing bound, or null when it has none.</param>
/// <param name="Dropoff">Where it is going.</param>
/// <param name="DriverName">The assigned driver's name, or null when nobody is assigned.</param>
/// <param name="IsOverdue">Whether the window has already passed.</param>
/// <param name="Status">Where it is in the lifecycle.</param>
public sealed record DashboardDelivery(
    int Id,
    DateTimeOffset? WindowEarliestAt,
    DateTimeOffset? WindowLatestAt,
    LocationView Dropoff,
    string? DriverName,
    bool IsOverdue,
    DeliveryStatus Status);
