namespace DriveTrack.Domain.Vehicles;

/// <summary>
/// A vehicle in the fleet. At most one driver may hold it at a time — enforced by a filtered
/// unique index on <c>drivers.vehicle_id</c>, not by an application check.
/// </summary>
public sealed class Vehicle
{
    /// <summary>Surrogate key. Plain <see cref="int"/> — only the three identity kinds are typed.</summary>
    public int Id { get; set; }

    /// <summary>Make and model as displayed to a dispatcher.</summary>
    public required string Model { get; set; }

    /// <summary>
    /// Registration plate. Unique across the fleet. Bounded and trimmed, with no format
    /// rule — the original's exact-eight-character rule is deliberately not carried forward.
    /// </summary>
    public required string LicensePlate { get; set; }

    /// <summary>Payload capacity. The unit is in the name, so no column comment has to carry it.</summary>
    public decimal CapacityKg { get; set; }

    /// <summary>
    /// Odometer reading. Knowingly-unread data: no screen displays it yet, and it is recorded
    /// rather than dropped so a later one can.
    /// </summary>
    public int Mileage { get; set; }

    /// <summary>
    /// When servicing is next due. A <see cref="DateOnly"/> rather than a timestamp: a due date
    /// has no time of day and no zone, so the UTC timestamp rule does not apply to it.
    /// Knowingly unread, for the same reason as <see cref="Mileage"/>.
    /// </summary>
    public DateOnly? NextMaintenanceDate { get; set; }
}
