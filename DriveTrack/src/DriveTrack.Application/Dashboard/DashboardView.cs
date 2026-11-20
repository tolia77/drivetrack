using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;

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
