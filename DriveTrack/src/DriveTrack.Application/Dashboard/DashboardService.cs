using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Common;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Dashboard;

/// <summary>
/// The day at a glance for the two roles that run dispatch. Read-only: it writes nothing and owns
/// no entity, so AD-24's ownership rule - which is about writes - has nothing to say about the
/// repositories it reads.
/// <para>
/// One unit of work, and every read in it awaited in turn. The context behind a unit of work is not
/// safe for concurrent use, so the reads cannot be fanned out with <c>Task.WhenAll</c>; and a unit
/// of work per card would be a transaction per card for a screen that is one snapshot of one moment.
/// </para>
/// <para>
/// <b>Chat (AD-16).</b> The conversations card reads the newest message per driver through
/// <c>IMessageRepository</c>, which makes this file one of chat's named seams in
/// <c>SeverabilityTests</c>: deleting the chat module means deleting that card's three lines here.
/// </para>
/// </summary>
public sealed class DashboardService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IAccessGuard accessGuard,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>How many of today's deliveries the card lists.</summary>
    public const int TodayLimit = 5;

    /// <summary>How many drivers the roster card lists.</summary>
    public const int DriverLimit = 5;

    /// <summary>How many timeline entries the activity card lists.</summary>
    public const int ActivityLimit = 5;

    /// <summary>How many conversations the conversations card lists.</summary>
    public const int ConversationLimit = 3;

    /// <summary>How many vehicles the fleet card lists.</summary>
    public const int VehicleLimit = 3;

    /// <summary>How many reviews the reviews card lists.</summary>
    public const int ReviewLimit = 2;

    /// <inheritdoc />
    public async Task<DashboardView> GetAsync(CancellationToken cancellationToken)
    {
        // AD-4: Dispatcher admits an administrator by rule. Asked before anything is read.
        accessGuard.RequireRole(UserRole.Dispatcher);

        var now = timeProvider.GetUtcNow();
        var zone = timeProvider.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var (todayStart, todayEnd) = LocalDay(localNow, zone);

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var me = await unitOfWork.Users.GetByIdAsync(currentUser.UserId, cancellationToken);

        // ---- The tiles and the warning. ---------------------------------------------------
        var byStatus = await unitOfWork.Deliveries.CountByStatusAsync(cancellationToken);
        var overdue = await unitOfWork.Deliveries.CountOverdueAsync(now, cancellationToken);
        var unassigned = await unitOfWork.Deliveries.CountUnassignedOpenAsync(cancellationToken);

        var deliveredToday = await unitOfWork.TimelineEntries.ListDeliveryIdsMovedToAsync(
            DeliveryStatus.Delivered,
            todayStart,
            todayEnd,
            cancellationToken);

        var withProof = await unitOfWork.ProofOfDeliveries.CountForDeliveriesAsync(
            deliveredToday,
            cancellationToken);

        var onDuty = (await unitOfWork.Shifts.ListOpenDriverIdsAsync(cancellationToken)).ToHashSet();
        var firstShift = await unitOfWork.Shifts.FindEarliestOpenStartAsync(cancellationToken);

        // ---- The people every card names. -------------------------------------------------
        var driverNames = Names(
            await unitOfWork.Users.ListByRoleAsync(UserRole.Driver, cancellationToken),
            account => account.DriverId);

        var clientNames = Names(
            await unitOfWork.Users.ListByRoleAsync(UserRole.Client, cancellationToken),
            account => account.ClientId);

        // ---- The cards. -------------------------------------------------------------------
        var today = await unitOfWork.Deliveries.ListOpenStartingBetweenAsync(
            todayStart,
            todayEnd,
            TodayLimit,
            cancellationToken);

        var drivers = await unitOfWork.Drivers.ListAsync(cancellationToken);
        var ratings = (await unitOfWork.Reviews.ListDriverRatingsAsync(
                [.. drivers.Select(driver => driver.Id)],
                cancellationToken))
            .ToDictionary(rating => rating.DriverId);

        var activity = await unitOfWork.TimelineEntries.ListLatestAsync(ActivityLimit, cancellationToken);

        var reviews = await unitOfWork.Reviews.ListAsync(
            new AccessScope(null, null),
            0,
            ReviewLimit,
            cancellationToken);

        // The deliveries the activity and the reviews refer to, read once for both cards.
        var referenced = (await unitOfWork.Deliveries.ListByIdsAsync(
                new AccessScope(null, null),
                [.. activity.Select(entry => entry.DeliveryId).Concat(reviews.Select(review => review.DeliveryId))],
                cancellationToken))
            .ToDictionary(delivery => delivery.Id);

        // Chat is a dispatcher's conversation with a driver; an administrator takes no part in it
        // (AccessGuard.RequireChatParticipant), so they are offered neither the card nor the link.
        IReadOnlyList<DashboardConversation>? conversations = null;

        if (currentUser.Role == UserRole.Dispatcher)
        {
            conversations =
            [
                .. (await unitOfWork.Messages.ListLatestPerDriverAsync(ConversationLimit, cancellationToken))
                    .Select(message => new DashboardConversation(
                        message.DriverId,
                        Name(driverNames, message.DriverId),
                        message.Text,
                        message.SentAt)),
            ];
        }

        var vehicles = await unitOfWork.Vehicles.ListByNextMaintenanceAsync(VehicleLimit, cancellationToken);

        int Count(DeliveryStatus status) =>
            byStatus.FirstOrDefault(row => row.Status == status)?.Count ?? 0;

        return new DashboardView(
            me?.FirstName ?? string.Empty,
            localNow,
            Count(DeliveryStatus.Pending),
            Count(DeliveryStatus.InTransit),
            overdue,
            unassigned,
            deliveredToday.Count,
            withProof,
            drivers.Count(driver => onDuty.Contains(driver.Id)),
            drivers.Count,
            firstShift,
            [
                .. today.Select(delivery => new DashboardDelivery(
                    delivery.Id,
                    delivery.WindowEarliestAt,
                    delivery.WindowLatestAt,
                    View(delivery.DropoffLocation),
                    delivery.DriverId is { } driverId ? Name(driverNames, driverId) : null,
                    OverdueRule.IsOverdue(delivery, now),
                    delivery.Status)),
            ],
            [
                .. drivers
                    .Select(driver =>
                    {
                        // Absent means nobody has reviewed them: no average, and no zero standing
                        // in for one.
                        var rating = ratings.GetValueOrDefault(driver.Id);

                        return new DashboardDriver(
                            driver.Id,
                            Name(driverNames, driver.Id),
                            driver.Vehicle?.Model,
                            driver.Vehicle?.LicensePlate,
                            rating?.Average,
                            rating?.ReviewCount ?? 0,
                            onDuty.Contains(driver.Id));
                    })
                    // On duty first - they are who a dispatcher can hand work to now - then by
                    // name, ordinally so the order is the same on every machine, then by id so two
                    // drivers sharing a name do not swap places between reads.
                    .OrderByDescending(driver => driver.OnDuty)
                    .ThenBy(driver => driver.Name, StringComparer.Ordinal)
                    .ThenBy(driver => driver.Id.Value)
                    .Take(DriverLimit),
            ],
            [
                .. activity.Select(entry => new DashboardActivity(
                    entry.Id,
                    entry.ActorDisplayName,
                    entry.ActorRole,
                    KindOf(entry),
                    entry.PreviousStatus,
                    entry.NewStatus,
                    entry.Note,
                    referenced.TryGetValue(entry.DeliveryId, out var delivery)
                        ? View(delivery.DropoffLocation)
                        : null,
                    entry.OccurredAt)),
            ],
            conversations,
            [
                .. vehicles.Select(vehicle => new DashboardVehicle(
                    vehicle.Id,
                    vehicle.Model,
                    vehicle.LicensePlate,
                    vehicle.NextMaintenanceDate,
                    vehicle.Mileage)),
            ],
            [
                .. reviews.Select(review => new DashboardReview(
                    review.Id,
                    review.Rating,
                    Name(clientNames, review.ClientId),
                    referenced.TryGetValue(review.DeliveryId, out var reviewed)
                        && reviewed.DriverId is { } reviewedDriver
                        ? Name(driverNames, reviewedDriver)
                        : null,
                    review.Text,
                    review.CreatedAt)),
            ]);
    }

    /// <summary>
    /// The local calendar day <paramref name="localNow"/> falls in, as two instants: its first
    /// moment and the first moment of the next day. Each bound takes the offset the zone has at that
    /// bound, so a day that crosses a daylight-saving change is still exactly one day long.
    /// </summary>
    private static (DateTimeOffset Start, DateTimeOffset End) LocalDay(
        DateTimeOffset localNow,
        TimeZoneInfo zone)
    {
        var midnight = localNow.Date;
        var next = midnight.AddDays(1);

        return (
            new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)),
            new DateTimeOffset(next, zone.GetUtcOffset(next)));
    }

    private static DashboardActivityKind KindOf(TimelineEntry entry) =>
        (entry.PreviousStatus, entry.NewStatus) switch
        {
            (null, null) => DashboardActivityKind.Comment,
            (null, _) => DashboardActivityKind.Created,
            _ => DashboardActivityKind.StatusChange,
        };

    private static Dictionary<TKey, string> Names<TKey>(
        IReadOnlyList<UserAccount> accounts,
        Func<UserAccount, TKey?> key)
        where TKey : struct
    {
        var names = new Dictionary<TKey, string>();

        foreach (var account in accounts)
        {
            if (key(account) is { } id)
            {
                names[id] = (account.FirstName + " " + account.LastName).Trim();
            }
        }

        return names;
    }

    private static string Name<TKey>(Dictionary<TKey, string> names, TKey id)
        where TKey : notnull =>
        names.TryGetValue(id, out var name) ? name : string.Empty;

    private static LocationView View(Location location) =>
        new(new MapLocation(location.Latitude, location.Longitude), location.Address);
}
