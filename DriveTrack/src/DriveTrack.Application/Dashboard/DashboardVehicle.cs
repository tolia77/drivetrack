namespace DriveTrack.Application.Dashboard;

/// <summary>One vehicle on the fleet card.</summary>
/// <param name="Id">The vehicle's id.</param>
/// <param name="Model">Its model.</param>
/// <param name="LicensePlate">Its plate.</param>
/// <param name="NextMaintenanceDate">When it is next due a service, or null when unrecorded.</param>
/// <param name="Mileage">Its recorded mileage, in kilometres.</param>
public sealed record DashboardVehicle(
    int Id,
    string Model,
    string LicensePlate,
    DateOnly? NextMaintenanceDate,
    int Mileage);
