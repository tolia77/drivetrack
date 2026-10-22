using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Dashboard;
using DriveTrack.Domain.Chat;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Shifts;
using DriveTrack.Domain.Vehicles;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;

namespace DriveTrack.Integration.Tests.Dashboard;

/// <summary>
/// The dispatcher's dashboard against a real PostgreSQL 18, with the clock fixed in Kyiv.
/// <para>
/// The service is built directly over the test database rather than behind a host: what is under
/// test is which rows each figure counts, and a host would add a sign-in to every case for a
/// question the screen tests already answer.
/// </para>
/// </summary>
public class DashboardServiceTests(PostgresFixture postgres)
{
    private const string Password = "Passw0rd-Test";

    /// <summary>
    /// 12:30 in Kyiv on Thursday 22 October 2026 - still summer time (UTC+3) until the 25th, so
    /// the local day runs from 21:00 UTC on the 21st to 21:00 UTC on the 22nd. A day cut at UTC
    /// midnight would get both edges below wrong.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TodayStart = new(2026, 10, 21, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TomorrowStart = new(2026, 10, 22, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_tiles_count_the_local_day_and_the_overdue_rule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);
        var olena = await world.DriverAsync("Олена", "Коваленко", vehicleId: null, cancellationToken);

        // Open, starting on the very first instant of the Kyiv day - which is still the 21st in UTC
        // - with a window already shut and no driver: today, overdue and unassigned at once.
        var first = await world.DeliveryAsync(
            Seed.NewDelivery(windowEarliestAt: TodayStart, windowLatestAt: TodayStart.AddHours(2)),
            cancellationToken);

        // On the road, starting a minute before the day ends.
        var last = await world.DeliveryAsync(
            Seed.NewDelivery(
                driverId: olena.DriverId,
                windowEarliestAt: TomorrowStart.AddMinutes(-1),
                windowLatestAt: TomorrowStart.AddHours(1),
                status: DeliveryStatus.InTransit),
            cancellationToken);

        // Starting on the first instant of tomorrow: not today, however close.
        await world.DeliveryAsync(
            Seed.NewDelivery(windowEarliestAt: TomorrowStart),
            cancellationToken);

        // Starting a minute before today began, with a driver: open and on the board, not today.
        await world.DeliveryAsync(
            Seed.NewDelivery(driverId: olena.DriverId, windowEarliestAt: TodayStart.AddMinutes(-1)),
            cancellationToken);

        // Failed with its window shut: closed, so never overdue.
        await world.DeliveryAsync(
            Seed.NewDelivery(
                driverId: olena.DriverId,
                windowLatestAt: Now.AddHours(-1),
                status: DeliveryStatus.Failed),
            cancellationToken);

        // Delivered long after its window: never overdue, because it arrived.
        var deliveredWithProof = await world.DeliveryAsync(
            Seed.NewDelivery(
                driverId: olena.DriverId,
                windowLatestAt: Now.AddDays(-2),
                status: DeliveryStatus.Delivered),
            cancellationToken);

        var deliveredWithoutProof = await world.DeliveryAsync(
            Seed.NewDelivery(driverId: olena.DriverId, status: DeliveryStatus.Delivered),
            cancellationToken);

        // Delivered, with a window opening today and a driver: no longer open, so not on today's list.
        await world.DeliveryAsync(
            Seed.NewDelivery(
                driverId: olena.DriverId,
                windowEarliestAt: TodayStart.AddHours(3),
                windowLatestAt: TomorrowStart.AddHours(1),
                status: DeliveryStatus.Delivered),
            cancellationToken);

        // Delivered with no driver: not open, so not unassigned work.
        await world.DeliveryAsync(
            Seed.NewDelivery(status: DeliveryStatus.Delivered),
            cancellationToken);

        var deliveredYesterday = await world.DeliveryAsync(
            Seed.NewDelivery(driverId: olena.DriverId, status: DeliveryStatus.Delivered),
            cancellationToken);

        await using (var context = await world.Database.CreateContextAsync(cancellationToken))
        {
            // Both edges of the day, again: the first instant counts and the instant before it
            // does not.
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveredWithProof, null, DeliveryStatus.InTransit, DeliveryStatus.Delivered, occurredAt: TodayStart));
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveredWithoutProof, null, DeliveryStatus.InTransit, DeliveryStatus.Delivered, occurredAt: Now));
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveredYesterday, null, DeliveryStatus.InTransit, DeliveryStatus.Delivered, occurredAt: TodayStart.AddSeconds(-1)));

            context.ProofOfDeliveries.Add(Proof(deliveredWithProof));
            context.ProofOfDeliveries.Add(Proof(deliveredYesterday));

            await context.SaveChangesAsync(cancellationToken);
        }

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        Assert.Equal("Ірина", view.FirstName);
        Assert.Equal(TimeSpan.FromHours(3), view.LocalNow.Offset);
        Assert.Equal(12, view.LocalNow.Hour);

        Assert.Equal(3, view.Pending);
        Assert.Equal(1, view.InTransit);
        Assert.Equal(4, view.Open);
        Assert.Equal(1, view.Overdue);
        Assert.Equal(2, view.UnassignedOpen);

        Assert.Equal(2, view.DeliveredToday);
        Assert.Equal(1, view.DeliveredTodayWithProof);

        // Today's list: both edges in, earliest window first, and each row says what it is.
        Assert.Equal([first, last], view.Today.Select(row => row.Id));

        var head = view.Today[0];
        Assert.Null(head.DriverName);
        Assert.True(head.IsOverdue);
        Assert.Equal(DeliveryStatus.Pending, head.Status);

        var tail = view.Today[1];
        Assert.Equal("Олена Коваленко", tail.DriverName);
        Assert.False(tail.IsOverdue);
        Assert.Equal(DeliveryStatus.InTransit, tail.Status);
    }

    [Fact]
    public async Task The_day_summer_time_ends_is_twenty_five_hours_long_and_counted_whole()
    {
        // 25 October 2026: Kyiv leaves summer time at 04:00 local, so the local day runs from 21:00
        // UTC on the 24th (UTC+3) to 22:00 UTC on the 25th (UTC+2). A day built from one offset
        // would drop the last hour or take in an hour of the next day.
        var cancellationToken = TestContext.Current.CancellationToken;
        var start = new DateTimeOffset(2026, 10, 24, 21, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 25, 22, 0, 0, TimeSpan.Zero);

        await using var world = await World.CreateAsync(
            postgres.ConnectionString,
            cancellationToken,
            now: new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);

        var instants = new[] { start, end.AddSeconds(-1), start.AddSeconds(-1), end };

        await using (var context = await world.Database.CreateContextAsync(cancellationToken))
        {
            foreach (var instant in instants)
            {
                var delivery = Seed.NewDelivery(status: DeliveryStatus.Delivered);

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                context.TimelineEntries.Add(Seed.NewTimelineEntry(
                    delivery.Id, null, DeliveryStatus.InTransit, DeliveryStatus.Delivered, occurredAt: instant));
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        // The first instant and the last second of the long day; neither neighbour outside it.
        Assert.Equal(2, view.DeliveredToday);
        Assert.Equal(TimeSpan.FromHours(2), view.LocalNow.Offset);
    }

    [Fact]
    public async Task An_empty_system_reads_as_zeros_and_empty_lists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        Assert.Equal(0, view.Open);
        Assert.Equal(0, view.Overdue);
        Assert.Equal(0, view.UnassignedOpen);
        Assert.Equal(0, view.DeliveredToday);
        Assert.Equal(0, view.DeliveredTodayWithProof);
        Assert.Equal(0, view.DriversOnDuty);
        Assert.Equal(0, view.DriversTotal);
        Assert.Null(view.FirstOpenShiftStartedAt);

        Assert.Empty(view.Today);
        Assert.Empty(view.Drivers);
        Assert.Empty(view.Activity);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<DashboardConversation>>(view.Conversations));
        Assert.Empty(view.Vehicles);
        Assert.Empty(view.Reviews);
    }

    [Fact]
    public async Task Drivers_on_duty_come_first_and_the_first_open_shift_is_named()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);

        var vehicle = await world.VehicleAsync("AA 1234 BK", new DateOnly(2026, 12, 1), cancellationToken);

        // Alphabetically first, and off duty: the order is duty before name.
        var andrii = await world.DriverAsync("Андрій", "Мельник", vehicle.Id, cancellationToken);
        var taras = await world.DriverAsync("Тарас", "Мельник", vehicleId: null, cancellationToken);
        var olena = await world.DriverAsync("Олена", "Коваленко", vehicleId: null, cancellationToken);

        var firstShift = new DateTimeOffset(2026, 10, 22, 5, 0, 0, TimeSpan.Zero);

        await using (var context = await world.Database.CreateContextAsync(cancellationToken))
        {
            context.Shifts.Add(new Shift { DriverId = taras.DriverId, StartedAt = firstShift.AddHours(1) });
            context.Shifts.Add(new Shift { DriverId = olena.DriverId, StartedAt = firstShift });

            // A finished shift is not duty, however early it began.
            context.Shifts.Add(new Shift
            {
                DriverId = andrii.DriverId,
                StartedAt = firstShift.AddHours(-3),
                EndedAt = firstShift.AddHours(-1),
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        Assert.Equal(2, view.DriversOnDuty);
        Assert.Equal(3, view.DriversTotal);
        Assert.Equal(firstShift, view.FirstOpenShiftStartedAt);

        Assert.Equal(
            ["Олена Коваленко", "Тарас Мельник", "Андрій Мельник"],
            view.Drivers.Select(driver => driver.Name));

        var offDuty = view.Drivers[2];
        Assert.False(offDuty.OnDuty);
        Assert.Equal("Renault Master", offDuty.VehicleModel);
        Assert.Equal("AA 1234 BK", offDuty.VehicleLicensePlate);
        Assert.Null(offDuty.Rating);
        Assert.Equal(0, offDuty.ReviewCount);
    }

    [Fact]
    public async Task Vehicles_are_listed_by_their_next_service_with_the_undated_last()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);

        await world.VehicleAsync("UNDATED", null, cancellationToken);
        await world.VehicleAsync("LATER", new DateOnly(2026, 12, 1), cancellationToken);
        await world.VehicleAsync("SOONER", new DateOnly(2026, 10, 20), cancellationToken);
        await world.VehicleAsync("UNDATED2", null, cancellationToken);

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        // Three, soonest first, and an undated vehicle only once every dated one is listed.
        Assert.Equal(
            ["SOONER", "LATER", "UNDATED"],
            view.Vehicles.Select(vehicle => vehicle.LicensePlate));

        Assert.Null(view.Vehicles[2].NextMaintenanceDate);
    }

    [Fact]
    public async Task The_activity_reviews_and_conversations_name_who_they_are_about()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var dispatcher = await world.AccountAsync("Ірина", "Бондар", UserRole.Dispatcher, cancellationToken);
        var olena = await world.DriverAsync("Олена", "Коваленко", vehicleId: null, cancellationToken);
        var taras = await world.DriverAsync("Тарас", "Мельник", vehicleId: null, cancellationToken);
        var client = await world.ClientAsync("Андрій", "Шевчук", cancellationToken);

        var delivery = Seed.NewDelivery(
            clientId: client.ClientId,
            driverId: olena.DriverId,
            status: DeliveryStatus.Delivered);

        delivery.DropoffLocation = new DriveTrack.Domain.Common.Location(50.45, 30.52, "вул. Хрещатик, 22", null);

        var deliveryId = await world.DeliveryAsync(delivery, cancellationToken);

        await using (var context = await world.Database.CreateContextAsync(cancellationToken))
        {
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveryId, null, null, DeliveryStatus.Pending, occurredAt: Now.AddHours(-3)));
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveryId, null, DeliveryStatus.InTransit, DeliveryStatus.Delivered, occurredAt: Now.AddHours(-2)));
            context.TimelineEntries.Add(Seed.NewTimelineEntry(
                deliveryId, null, null, null, note: "Клієнт просив зателефонувати", occurredAt: Now.AddHours(-1)));

            context.Reviews.Add(Seed.NewReview(deliveryId, client.ClientId, rating: 5, text: "Вчасно"));

            context.Messages.Add(new Message { DriverId = olena.DriverId, Text = "Старе", SentAt = Now.AddHours(-3) });
            context.Messages.Add(new Message { DriverId = olena.DriverId, Text = "Нове", SentAt = Now.AddMinutes(-5) });
            context.Messages.Add(new Message { DriverId = taras.DriverId, Text = "Тарасове", SentAt = Now.AddHours(-1) });

            await context.SaveChangesAsync(cancellationToken);
        }

        var view = await world.ReadAsync(dispatcher, UserRole.Dispatcher, cancellationToken);

        // Newest first, each entry classified by what it moved.
        Assert.Equal(
            [DashboardActivityKind.Comment, DashboardActivityKind.StatusChange, DashboardActivityKind.Created],
            view.Activity.Select(entry => entry.Kind));
        Assert.All(view.Activity, entry => Assert.Equal("вул. Хрещатик, 22", entry.Dropoff?.Address));
        Assert.Equal("Клієнт просив зателефонувати", view.Activity[0].Note);

        var review = Assert.Single(view.Reviews);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Андрій Шевчук", review.ClientName);
        Assert.Equal("Олена Коваленко", review.DriverName);

        // One line per driver - the newest - and the most recently spoken conversation first.
        var conversations = Assert.IsAssignableFrom<IReadOnlyList<DashboardConversation>>(view.Conversations);

        Assert.Equal(
            [("Олена Коваленко", "Нове"), ("Тарас Мельник", "Тарасове")],
            conversations.Select(line => (line.DriverName, line.Text)));
    }

    [Fact]
    public async Task An_administrator_reads_the_dashboard_without_the_conversations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var admin = await world.AccountAsync("Адміністратор", "Системи", UserRole.Admin, cancellationToken);
        var olena = await world.DriverAsync("Олена", "Коваленко", vehicleId: null, cancellationToken);

        await using (var context = await world.Database.CreateContextAsync(cancellationToken))
        {
            context.Messages.Add(new Message { DriverId = olena.DriverId, Text = "Привіт", SentAt = Now });
            await context.SaveChangesAsync(cancellationToken);
        }

        var view = await world.ReadAsync(admin, UserRole.Admin, cancellationToken);

        // AD-4 admits the administrator; chat is the one place they take no part.
        Assert.Equal("Адміністратор", view.FirstName);
        Assert.Null(view.Conversations);
        Assert.Equal(1, view.DriversTotal);
    }

    [Theory]
    [InlineData(UserRole.Driver)]
    [InlineData(UserRole.Client)]
    public async Task A_driver_or_a_client_is_refused(UserRole role)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await World.CreateAsync(postgres.ConnectionString, cancellationToken);

        var account = await world.AccountAsync("Петро", "Шевченко", role, cancellationToken);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(
            () => world.ReadAsync(account, role, cancellationToken));

        Assert.Equal(ErrorCode.AUTH_FORBIDDEN, refusal.Code);
    }

    private static ProofOfDelivery Proof(int deliveryId) =>
        new()
        {
            DeliveryId = deliveryId,
            RecipientName = "Отримувач",
            CaptureLocation = Seed.Location(),
            CapturedAt = Now,
        };

    /// <summary>One test database, a clock fixed in Kyiv, and the rows a case writes into it.</summary>
    private sealed class World : IAsyncDisposable
    {
        private readonly FixedTimeProvider _clock;

        private World(TestDatabase database, DateTimeOffset now)
        {
            Database = database;
            _clock = new FixedTimeProvider(now)
            {
                Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv"),
            };
        }

        public TestDatabase Database { get; }

        public static async Task<World> CreateAsync(
            string connectionString,
            CancellationToken cancellationToken,
            DateTimeOffset? now = null) =>
            new(await TestDatabase.CreateAsync(connectionString, cancellationToken), now ?? Now);

        public Task<DashboardView> ReadAsync(UserId userId, UserRole role, CancellationToken cancellationToken)
        {
            var caller = new Caller(userId, role);

            var service = new DashboardService(
                Database.UnitOfWorkFactory,
                new AccessGuard(caller),
                caller,
                _clock);

            return service.GetAsync(cancellationToken);
        }

        public async Task<UserId> AccountAsync(
            string firstName,
            string lastName,
            UserRole role,
            CancellationToken cancellationToken) =>
            (await CreateAccountAsync(firstName, lastName, role, cancellationToken)).Id;

        public async Task<(UserId UserId, DriverId DriverId)> DriverAsync(
            string firstName,
            string lastName,
            int? vehicleId,
            CancellationToken cancellationToken)
        {
            var account = await CreateAccountAsync(firstName, lastName, UserRole.Driver, cancellationToken);

            await using var context = await Database.CreateContextAsync(cancellationToken);

            var driver = new Driver
            {
                UserId = account.Id,
                LicenseNumber = Guid.NewGuid().ToString("N")[..10],
                VehicleId = vehicleId,
            };

            context.Drivers.Add(driver);
            await context.SaveChangesAsync(cancellationToken);

            return (account.Id, driver.Id);
        }

        public async Task<(UserId UserId, ClientId ClientId)> ClientAsync(
            string firstName,
            string lastName,
            CancellationToken cancellationToken)
        {
            var account = await CreateAccountAsync(firstName, lastName, UserRole.Client, cancellationToken);

            await using var context = await Database.CreateContextAsync(cancellationToken);

            var client = new Client { UserId = account.Id, PhoneNumber = "+380441234567" };

            context.Clients.Add(client);
            await context.SaveChangesAsync(cancellationToken);

            return (account.Id, client.Id);
        }

        public async Task<Vehicle> VehicleAsync(
            string licensePlate,
            DateOnly? nextMaintenanceDate,
            CancellationToken cancellationToken)
        {
            await using var context = await Database.CreateContextAsync(cancellationToken);

            var vehicle = new Vehicle
            {
                Model = "Renault Master",
                LicensePlate = licensePlate,
                CapacityKg = 1200m,
                Mileage = 48_210,
                NextMaintenanceDate = nextMaintenanceDate,
            };

            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync(cancellationToken);

            return vehicle;
        }

        public async Task<int> DeliveryAsync(Delivery delivery, CancellationToken cancellationToken)
        {
            await using var context = await Database.CreateContextAsync(cancellationToken);

            context.Deliveries.Add(delivery);
            await context.SaveChangesAsync(cancellationToken);

            return delivery.Id;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();

        private async Task<UserAccount> CreateAccountAsync(
            string firstName,
            string lastName,
            UserRole role,
            CancellationToken cancellationToken)
        {
            await using var unitOfWork = await Database.UnitOfWorkFactory.CreateAsync(cancellationToken);

            await unitOfWork.Users.EnsureRoleAsync(role, cancellationToken);

            var account = await unitOfWork.Users.CreateAsync(
                new NewUserAccount(firstName, lastName, Guid.NewGuid().ToString("N")[..12] + "@drivetrack.test"),
                Password,
                role,
                cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);

            return account;
        }
    }

    private sealed class Caller(UserId userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public UserId UserId => userId;

        public UserRole Role => role;

        public DriverId? DriverId => null;

        public ClientId? ClientId => null;
    }
}
