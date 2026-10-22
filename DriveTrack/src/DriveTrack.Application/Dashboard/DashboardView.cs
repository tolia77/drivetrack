using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Dashboard;

/// <summary>
/// The dispatcher's day, as the dashboard draws it: the counts behind the four tiles and the
/// warning, and the short lists behind each card. Read-only, and every figure is derived from rows
/// that already exist - nothing here is stored.
/// </summary>
/// <param name="FirstName">The caller's own first name, for the greeting.</param>
/// <param name="LocalNow">
/// The instant the view was read at, in <see cref="TimeProvider.LocalTimeZone"/>. The greeting and
/// the date line are read from it, so they agree with the "today" every count below was cut at.
/// </param>
/// <param name="Pending">Deliveries waiting to be picked up.</param>
/// <param name="InTransit">Deliveries on the road.</param>
/// <param name="Overdue">Deliveries overdue now, by <see cref="DriveTrack.Application.Deliveries.OverdueRule"/>.</param>
/// <param name="UnassignedOpen">Open deliveries with no driver.</param>
/// <param name="DeliveredToday">Deliveries a timeline entry moved to Delivered today.</param>
/// <param name="DeliveredTodayWithProof">How many of those have a proof captured.</param>
/// <param name="DriversOnDuty">Drivers with an open shift.</param>
/// <param name="DriversTotal">Every driver on the roster.</param>
/// <param name="FirstOpenShiftStartedAt">When the earliest open shift started, or null.</param>
/// <param name="Today">The next open deliveries whose window starts today.</param>
/// <param name="Drivers">A few drivers, the ones on duty first.</param>
/// <param name="Activity">The newest timeline entries across every delivery.</param>
/// <param name="Conversations">
/// The newest message per driver, or null for a caller who takes no part in chat - an
/// administrator, who is offered neither the card nor the way into the conversations.
/// </param>
/// <param name="Vehicles">The vehicles whose service is due soonest.</param>
/// <param name="Reviews">The newest reviews.</param>
public sealed record DashboardView(
    string FirstName,
    DateTimeOffset LocalNow,
    int Pending,
    int InTransit,
    int Overdue,
    int UnassignedOpen,
    int DeliveredToday,
    int DeliveredTodayWithProof,
    int DriversOnDuty,
    int DriversTotal,
    DateTimeOffset? FirstOpenShiftStartedAt,
    IReadOnlyList<DashboardDelivery> Today,
    IReadOnlyList<DashboardDriver> Drivers,
    IReadOnlyList<DashboardActivity> Activity,
    IReadOnlyList<DashboardConversation>? Conversations,
    IReadOnlyList<DashboardVehicle> Vehicles,
    IReadOnlyList<DashboardReview> Reviews)
{
    /// <summary>Every delivery still to be completed: Pending and In transit together.</summary>
    public int Open => Pending + InTransit;
}

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

/// <summary>What one timeline entry recorded.</summary>
public enum DashboardActivityKind
{
    /// <summary>The entry that brought a delivery into being: nowhere to its first status.</summary>
    Created,

    /// <summary>A note and nothing else: neither status is set.</summary>
    Comment,

    /// <summary>A move from one status to another, with or without a note.</summary>
    StatusChange,
}

/// <summary>One entry of the latest activity across every delivery.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="ActorName">Who acted, as the entry recorded them.</param>
/// <param name="ActorRole">The role they acted in.</param>
/// <param name="Kind">What the entry recorded.</param>
/// <param name="PreviousStatus">Where the delivery was, or null.</param>
/// <param name="NewStatus">Where it went, or null.</param>
/// <param name="Note">What was said, or null.</param>
/// <param name="Dropoff">The delivery's drop-off, or null when the delivery no longer reads.</param>
/// <param name="OccurredAt">When it happened.</param>
public sealed record DashboardActivity(
    int Id,
    string ActorName,
    UserRole ActorRole,
    DashboardActivityKind Kind,
    DeliveryStatus? PreviousStatus,
    DeliveryStatus? NewStatus,
    string? Note,
    LocationView? Dropoff,
    DateTimeOffset OccurredAt);

/// <summary>The newest message of one driver's conversation.</summary>
/// <param name="DriverId">The driver the conversation is keyed on.</param>
/// <param name="DriverName">The driver's display name.</param>
/// <param name="Text">What the newest message says.</param>
/// <param name="SentAt">When it was sent.</param>
public sealed record DashboardConversation(
    DriverId DriverId,
    string DriverName,
    string Text,
    DateTimeOffset SentAt);

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

/// <summary>One of the newest reviews.</summary>
/// <param name="Id">The review's id.</param>
/// <param name="Rating">The score, 1 to 5.</param>
/// <param name="ClientName">Who wrote it.</param>
/// <param name="DriverName">The driver it is about, or null when the delivery had none.</param>
/// <param name="Text">What it says.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record DashboardReview(
    int Id,
    int Rating,
    string ClientName,
    string? DriverName,
    string Text,
    DateTimeOffset CreatedAt);
