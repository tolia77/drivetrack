using DriveTrack.Application.Users;
using DriveTrack.Application.Reviews;
using DriveTrack.Application.Shifts;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Vehicles;
using System.Net;
using System.Text.Json;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Shifts;
using DriveTrack.Integration.Tests.Deliveries;
using DriveTrack.Integration.Tests.Fleet;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Notifications;
using DriveTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DriveTrack.Integration.Tests.Api;

/// <summary>
/// The other nine lists paged, searched and sorted by the database: the fleet, the driver roster,
/// the shifts, the reviews, the notification log and the two account rosters.
/// <para>
/// One class rather than a case in each capability's suite, because the claim is one claim made
/// nine times - the WHERE runs over every row, the COUNT counts what the WHERE left, the ORDER BY
/// ends on the id - and the delivery lists, which carry the most filters, have a class of their own
/// (<see cref="DeliveryListTests"/>). Seeded through the context and read through the REST route.
/// </para>
/// </summary>
public class ListPagingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_fleet_search_reaches_past_the_first_page_and_the_total_counts_what_matched()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            for (var index = 0; index < 120; index++)
            {
                var vehicle = await Seed.VehicleAsync(context, cancellationToken);

                // The last three are the only Sprinters, past the first page of a hundred.
                vehicle.Model = index >= 117 ? "Мерседес Спрінтер" : "Рено Мастер";
                vehicle.CapacityKg = 1000m + index;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var found = await PageAsync(client, dispatcher, "/api/vehicles?search=" + Uri.EscapeDataString("спрінтер"), cancellationToken);

        Assert.Equal(3, found.GetProperty("total").GetInt32());

        var all = await PageAsync(client, dispatcher, "/api/vehicles?limit=10", cancellationToken);

        Assert.Equal(120, all.GetProperty("total").GetInt32());

        // Sorted by capacity, heaviest first: the heaviest vehicle is the last one seeded.
        var heaviest = await PageAsync(client, dispatcher, "/api/vehicles?sort=capacityKg&desc=true&limit=1", cancellationToken);

        Assert.Equal(1119m, heaviest.GetProperty("items")[0].GetProperty("capacityKg").GetDecimal());
    }

    [Fact]
    public async Task The_driver_search_reads_the_name_the_address_the_licence_and_the_vehicle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var vehicle = await Seed.VehicleAsync(context, cancellationToken, licensePlate: "ВС5678ЕЕ");

            await DriverNamedAsync(context, "Тарас", "Шевченко", cancellationToken, vehicle.Id, licence: "ВІ123456");
            await DriverNamedAsync(context, "Іван", "Франко", cancellationToken);
            await DriverNamedAsync(context, "Леся", "Українка", cancellationToken);
        }

        Assert.Equal(1, await TotalAsync(client, dispatcher, "/api/drivers?search=" + Uri.EscapeDataString("шевченко"), cancellationToken));
        Assert.Equal(1, await TotalAsync(client, dispatcher, "/api/drivers?search=" + Uri.EscapeDataString("ВІ1234"), cancellationToken));
        Assert.Equal(1, await TotalAsync(client, dispatcher, "/api/drivers?search=" + Uri.EscapeDataString("вс5678"), cancellationToken));
        Assert.Equal(3, await TotalAsync(client, dispatcher, "/api/drivers?search=" + Uri.EscapeDataString("@drivetrack.test"), cancellationToken));
        Assert.Equal(0, await TotalAsync(client, dispatcher, "/api/drivers?search=" + Uri.EscapeDataString("Коваленко"), cancellationToken));

        var byLastName = await PageAsync(client, dispatcher, "/api/drivers?sort=lastName", cancellationToken);

        Assert.Equal(
            ["Українка", "Франко", "Шевченко"],
            byLastName.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("lastName").GetString()).ToArray());

        // A driver with no vehicle sorts to the far end whichever way the column points.
        var byVehicle = await PageAsync(client, dispatcher, "/api/drivers?sort=vehicle&desc=true", cancellationToken);

        Assert.Equal(
            "Шевченко",
            byVehicle.GetProperty("items")[0].GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task The_rating_sort_keeps_the_unrated_drivers_at_the_far_end_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var author = await Seed.ClientAsync(context, cancellationToken);

            var good = await DriverNamedAsync(context, "Ольга", "Добра", cancellationToken);
            var poor = await DriverNamedAsync(context, "Петро", "Слабкий", cancellationToken);
            await DriverNamedAsync(context, "Нестор", "Новий", cancellationToken);

            foreach (var (driver, rating) in new[] { (good, 5), (good, 4), (poor, 2) })
            {
                var delivery = Seed.NewDelivery(clientId: author.Id, driverId: driver, status: DeliveryStatus.Delivered);

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                context.Reviews.Add(Seed.NewReview(delivery.Id, author.Id, rating));
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        var best = await LastNamesAsync(client, dispatcher, "/api/drivers?sort=rating&desc=true", cancellationToken);
        var worst = await LastNamesAsync(client, dispatcher, "/api/drivers?sort=rating", cancellationToken);

        Assert.Equal(["Добра", "Слабкий", "Новий"], best);
        Assert.Equal(["Слабкий", "Добра", "Новий"], worst);
    }

    [Fact]
    public async Task A_driver_reads_a_total_of_their_own_shifts_and_dispatch_searches_by_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);
        var driver = await DeliveryApi.DriverAsync(client, dispatcher, vehicleId: null, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var other = await DriverNamedAsync(context, "Олег", "Коваленко", cancellationToken);

            for (var day = 0; day < 30; day++)
            {
                var start = Seed.Instant.AddDays(-day);

                context.Shifts.Add(new Shift { DriverId = new DriverId(driver.DriverId), StartedAt = start, EndedAt = start.AddHours(8) });
                context.Shifts.Add(new Shift { DriverId = other, StartedAt = start, EndedAt = start.AddHours(6) });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        // AD-3: a driver's narrowing comes first, so the total is theirs - never the roster's.
        var own = await PageAsync(client, driver.Token, "/api/shifts?limit=10&offset=20", cancellationToken);

        Assert.Equal(30, own.GetProperty("total").GetInt32());
        Assert.Equal(10, own.GetProperty("items").GetArrayLength());

        // Dispatch searches every shift by the driver's name.
        Assert.Equal(30, await TotalAsync(client, dispatcher, "/api/shifts?search=" + Uri.EscapeDataString("КОВАЛЕНКО"), cancellationToken));
        Assert.Equal(60, await TotalAsync(client, dispatcher, "/api/shifts", cancellationToken));

        // Oldest first when the start column is turned round: the earliest shift is reachable.
        var oldest = await PageAsync(client, driver.Token, "/api/shifts?sort=startedAt&limit=1", cancellationToken);

        Assert.Equal(
            Seed.Instant.AddDays(-29),
            oldest.GetProperty("items")[0].GetProperty("startedAt").GetDateTimeOffset());

        // And the one still running is found on its own, whatever page of history is on screen.
        var open = await PageAsync(client, driver.Token, "/api/shifts?open=true", cancellationToken);

        Assert.Equal(0, open.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_notification_log_is_newest_first_and_its_oldest_attempt_is_reachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var delivery = Seed.NewDelivery();

            context.Deliveries.Add(delivery);
            await context.SaveChangesAsync(cancellationToken);

            for (var index = 0; index < 130; index++)
            {
                context.NotificationAttempts.Add(new NotificationAttempt
                {
                    DeliveryId = delivery.Id,
                    Kind = NotificationKind.StatusChange,
                    Recipient = $"r{index:000}@drivetrack.test",
                    AttemptedAt = Seed.Instant.AddMinutes(index),
                    Outcome = index % 2 == 0 ? NotificationOutcome.Failed : NotificationOutcome.Sent,
                    Error = null,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var newest = await PageAsync(client, admin, "/api/notifications?limit=1", cancellationToken);

        Assert.Equal(130, newest.GetProperty("total").GetInt32());
        Assert.Equal("r129@drivetrack.test", newest.GetProperty("items")[0].GetProperty("recipient").GetString());

        var oldest = await PageAsync(client, admin, "/api/notifications?limit=10&offset=120", cancellationToken);

        Assert.Equal("r000@drivetrack.test", oldest.GetProperty("items")[9].GetProperty("recipient").GetString());

        // Outcome in declaration order - Sent before Failed - rather than alphabetically.
        var byOutcome = await PageAsync(client, admin, "/api/notifications?sort=outcome&limit=1", cancellationToken);

        Assert.Equal(nameof(NotificationOutcome.Sent), byOutcome.GetProperty("items")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task The_reviews_sort_over_every_review_and_an_author_cannot_sort_by_the_driver()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);
        var author = await DeliveryApi.ClientAsync(client, dispatcher, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            for (var index = 0; index < 12; index++)
            {
                var delivery = Seed.NewDelivery(clientId: new ClientId(author.ClientId), status: DeliveryStatus.Delivered);

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                context.Reviews.Add(Seed.NewReview(delivery.Id, new ClientId(author.ClientId), rating: (index % 5) + 1));
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var mine = await PageAsync(client, author.Token, "/api/reviews/mine?limit=5&offset=10", cancellationToken);

        Assert.Equal(12, mine.GetProperty("total").GetInt32());
        Assert.Equal(2, mine.GetProperty("items").GetArrayLength());

        var lowest = await PageAsync(client, dispatcher, "/api/reviews?sort=rating&limit=1", cancellationToken);

        Assert.Equal(1, lowest.GetProperty("items")[0].GetProperty("rating").GetInt32());

        // The author's list carries no party, so it is not sorted by one (AD-17).
        using var refused = await FleetApi.SendAsync(
            client, HttpMethod.Get, "/api/reviews/mine?sort=driver", author.Token, body: null, cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
    }

    [Fact]
    public async Task The_two_account_rosters_page_and_sort_and_count_only_their_own_role()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        for (var index = 0; index < 12; index++)
        {
            await FleetApi.TokenAsync(factory, client, UserRole.Client, cancellationToken);
        }

        for (var index = 0; index < 3; index++)
        {
            await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);
        }

        var clients = await PageAsync(client, admin, "/api/clients?limit=5&offset=10", cancellationToken);

        Assert.Equal(12, clients.GetProperty("total").GetInt32());
        Assert.Equal(2, clients.GetProperty("items").GetArrayLength());

        var dispatchers = await PageAsync(client, admin, "/api/dispatchers?sort=email&desc=true", cancellationToken);

        var emails = dispatchers.GetProperty("items")
            .EnumerateArray()
            .Select(row => row.GetProperty("email").GetString()!)
            .ToArray();

        Assert.Equal(3, dispatchers.GetProperty("total").GetInt32());
        Assert.Equal(emails.OrderDescending(StringComparer.Ordinal), emails);
    }

    [Fact]
    public async Task The_log_s_page_and_its_total_are_cut_by_the_database_rather_than_in_memory()
    {
        // DW-74: a repository that materialised the whole log and sliced it would answer every test
        // above correctly. What tells the two apart is the SQL, so the SQL is what this reads - the
        // page query carries the LIMIT and the OFFSET, and the total is a COUNT rather than a length.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await TestDatabase.CreateAsync(postgres.ConnectionString, cancellationToken);

        var statements = new List<string>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .LogTo(statements.Add, [RelationalEventId.CommandExecuted])
            .Options;

        await using var context = new AppDbContext(options);

        var repository = (INotificationAttemptRepository)Activator.CreateInstance(
            typeof(AppDbContext).Assembly.GetType(
                "DriveTrack.Infrastructure.Persistence.Repositories.EfNotificationAttemptRepository",
                throwOnError: true)!,
            context)!;

        await repository.ListAsync(new ListNotificationAttemptsQuery(0, 10), sort: null, cancellationToken);

        // An empty log answers its total without a page query at all.
        Assert.Contains(statements, statement => statement.Contains("count(*)", StringComparison.OrdinalIgnoreCase));

        var delivery = Seed.NewDelivery();

        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync(cancellationToken);

        context.NotificationAttempts.Add(new NotificationAttempt
        {
            DeliveryId = delivery.Id,
            Kind = NotificationKind.StatusChange,
            Recipient = "r@drivetrack.test",
            AttemptedAt = Seed.Instant,
            Outcome = NotificationOutcome.Sent,
        });
        await context.SaveChangesAsync(cancellationToken);

        statements.Clear();

        var page = await repository.ListAsync(new ListNotificationAttemptsQuery(0, 10), sort: null, cancellationToken);

        Assert.Equal(1, page.Total);
        Assert.Contains(
            statements,
            statement => statement.Contains("LIMIT", StringComparison.Ordinal)
                && statement.Contains("OFFSET", StringComparison.Ordinal)
                && statement.Contains("ORDER BY", StringComparison.Ordinal));
    }

    // =====================================================================================
    // Every sort column, walked
    //
    // A sort arm that goes missing falls through to the list's default order without a sound, so
    // each list's enum is walked member by member over rows seeded so that every column orders them
    // differently from the default - in both directions. The assertion is
    // DeliveryListTests.AssertEveryColumnReordersAsync, which also checks the walk is complete.
    // =====================================================================================

    [Fact]
    public async Task Every_column_of_the_fleet_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        var v = new int[3];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            (string Model, string Plate, decimal Capacity)[] rows = [("В", "BB", 100m), ("А", "CC", 300m), ("Б", "AA", 200m)];

            for (var index = 0; index < rows.Length; index++)
            {
                var vehicle = await Seed.VehicleAsync(context, cancellationToken, licensePlate: rows[index].Plate);

                vehicle.Model = rows[index].Model;
                vehicle.CapacityKg = rows[index].Capacity;
                await context.SaveChangesAsync(cancellationToken);

                v[index] = vehicle.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<VehicleSort, (int[] Ascending, int[] Descending)>
            {
                [VehicleSort.Model] = ([v[1], v[2], v[0]], [v[0], v[2], v[1]]),
                [VehicleSort.LicensePlate] = ([v[2], v[0], v[1]], [v[1], v[0], v[2]]),
                [VehicleSort.CapacityKg] = ([v[0], v[2], v[1]], [v[1], v[2], v[0]]),
            },
            skipped: [],
            query => DeliveryListTests.ReadIdsAsync(client, dispatcher, "/api/vehicles" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_of_the_driver_roster_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        var d = new int[4];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var author = await Seed.ClientAsync(context, cancellationToken);

            (string First, string Last, string Email, string Licence, string? Vehicle, int? Rating)[] rows =
            [
                ("В", "Г", "c@drivetrack.test", "L1", "Б", 4),
                ("А", "Б", "d@drivetrack.test", "L3", null, 2),
                ("Г", "В", "b@drivetrack.test", "L4", "А", null),
                ("Б", "А", "a@drivetrack.test", "L2", "В", 5),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                int? vehicleId = null;

                if (row.Vehicle is { } model)
                {
                    var vehicle = await Seed.VehicleAsync(context, cancellationToken);
                    vehicle.Model = model;
                    vehicleId = vehicle.Id;
                }

                var driver = await DriverNamedAsync(context, row.First, row.Last, cancellationToken, vehicleId, row.Licence);

                var user = await context.Users.SingleAsync(candidate => candidate.FirstName == row.First && candidate.LastName == row.Last, cancellationToken);
                user.Email = row.Email;

                if (row.Rating is { } rating)
                {
                    var delivery = Seed.NewDelivery(clientId: author.Id, driverId: driver, status: DeliveryStatus.Delivered);

                    context.Deliveries.Add(delivery);
                    await context.SaveChangesAsync(cancellationToken);

                    context.Reviews.Add(Seed.NewReview(delivery.Id, author.Id, rating));
                }

                await context.SaveChangesAsync(cancellationToken);

                d[index] = driver.Value;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<DriverSort, (int[] Ascending, int[] Descending)>
            {
                [DriverSort.FirstName] = ([d[1], d[3], d[0], d[2]], [d[2], d[0], d[3], d[1]]),
                [DriverSort.LastName] = ([d[3], d[1], d[2], d[0]], [d[0], d[2], d[1], d[3]]),
                [DriverSort.Email] = ([d[3], d[2], d[0], d[1]], [d[1], d[0], d[2], d[3]]),
                [DriverSort.LicenseNumber] = ([d[0], d[3], d[1], d[2]], [d[2], d[1], d[3], d[0]]),

                // No vehicle and no rating stay at the far end both ways.
                [DriverSort.Vehicle] = ([d[2], d[0], d[3], d[1]], [d[3], d[0], d[2], d[1]]),
                [DriverSort.Rating] = ([d[1], d[0], d[3], d[2]], [d[3], d[0], d[1], d[2]]),
            },
            skipped: [],
            query => DeliveryListTests.ReadIdsAsync(client, dispatcher, "/api/drivers" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_of_the_shift_list_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        var s = new int[4];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var day = Seed.Instant;

            (string Driver, DateTimeOffset Started, DateTimeOffset? Ended)[] rows =
            [
                ("А", day.AddDays(1), day.AddDays(1).AddHours(8)),
                ("В", day.AddDays(3), null),
                ("Б", day, day.AddDays(2).AddHours(5)),
                ("Г", day.AddDays(2), day.AddDays(2).AddHours(1)),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var driver = await DriverNamedAsync(context, rows[index].Driver, "Т", cancellationToken);
                var shift = new Shift { DriverId = driver, StartedAt = rows[index].Started, EndedAt = rows[index].Ended };

                context.Shifts.Add(shift);
                await context.SaveChangesAsync(cancellationToken);

                s[index] = shift.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<ShiftSort, (int[] Ascending, int[] Descending)>
            {
                [ShiftSort.Driver] = ([s[0], s[2], s[1], s[3]], [s[3], s[1], s[2], s[0]]),
                [ShiftSort.StartedAt] = ([s[2], s[0], s[3], s[1]], [s[1], s[3], s[0], s[2]]),

                // The one still running stays at the far end both ways.
                [ShiftSort.EndedAt] = ([s[0], s[3], s[2], s[1]], [s[2], s[3], s[0], s[1]]),
            },
            skipped: [],
            query => DeliveryListTests.ReadIdsAsync(client, dispatcher, "/api/shifts" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_of_the_moderation_list_reorders_the_rows_both_ways()
    {
        // The author's own list offers Delivery, Rating and CreatedAt over the same arms and refuses
        // the two party columns; that refusal is asserted above.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await FleetApi.TokenAsync(factory, client, UserRole.Dispatcher, cancellationToken);

        var r = new int[4];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Four deliveries first, in id order A < B < C < D, so the reviews can point at them
            // in an order of their own.
            var deliveries = new List<(int Id, DriverId Driver)>();

            foreach (var name in new[] { "Б", "А", "В", "Г" })
            {
                var driver = await DriverNamedAsync(context, name, "Т", cancellationToken);
                var delivery = Seed.NewDelivery(driverId: driver, status: DeliveryStatus.Delivered);

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                deliveries.Add((delivery.Id, driver));
            }

            // Review, its delivery, its author's first name, its rating and its age in hours. The
            // four reviews point at deliveries C, A, D and B, whose drivers are В, Б, Г and А.
            (int Delivery, string Author, int Rating, int Hours)[] rows =
            [
                (2, "Г", 2, 1),
                (0, "Б", 1, 3),
                (3, "В", 5, 0),
                (1, "А", 3, 2),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var author = await Seed.ClientAsync(context, cancellationToken);
                var user = await context.Users.FindAsync([author.UserId.Value], cancellationToken);
                user!.FirstName = rows[index].Author;
                user.LastName = "К";

                var target = deliveries[rows[index].Delivery];

                var review = Seed.NewReview(target.Id, author.Id, rows[index].Rating);
                review.CreatedAt = Seed.Instant.AddHours(rows[index].Hours);

                context.Reviews.Add(review);
                await context.SaveChangesAsync(cancellationToken);

                r[index] = review.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<ReviewSort, (int[] Ascending, int[] Descending)>
            {
                [ReviewSort.Delivery] = ([r[1], r[3], r[0], r[2]], [r[2], r[0], r[3], r[1]]),
                [ReviewSort.Client] = ([r[3], r[1], r[2], r[0]], [r[0], r[2], r[1], r[3]]),
                [ReviewSort.Driver] = ([r[3], r[1], r[0], r[2]], [r[2], r[0], r[1], r[3]]),
                [ReviewSort.Rating] = ([r[1], r[0], r[3], r[2]], [r[2], r[3], r[0], r[1]]),
                [ReviewSort.CreatedAt] = ([r[2], r[0], r[3], r[1]], [r[1], r[3], r[0], r[2]]),
            },
            skipped: [],
            query => DeliveryListTests.ReadIdsAsync(client, dispatcher, "/api/reviews" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_of_the_notification_log_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        var n = new int[4];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var deliveries = new List<int>();

            for (var index = 0; index < 4; index++)
            {
                var delivery = Seed.NewDelivery();

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                deliveries.Add(delivery.Id);
            }

            (int Delivery, string Recipient, NotificationOutcome Outcome, int Minutes)[] rows =
            [
                (1, "c@drivetrack.test", NotificationOutcome.Failed, 2),
                (0, "d@drivetrack.test", NotificationOutcome.Sent, 0),
                (3, "a@drivetrack.test", NotificationOutcome.Failed, 1),
                (2, "b@drivetrack.test", NotificationOutcome.Sent, 3),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var attempt = new NotificationAttempt
                {
                    DeliveryId = deliveries[rows[index].Delivery],
                    Kind = NotificationKind.StatusChange,
                    Recipient = rows[index].Recipient,
                    AttemptedAt = Seed.Instant.AddMinutes(rows[index].Minutes),
                    Outcome = rows[index].Outcome,
                };

                context.NotificationAttempts.Add(attempt);
                await context.SaveChangesAsync(cancellationToken);

                n[index] = attempt.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<NotificationSort, (int[] Ascending, int[] Descending)>
            {
                [NotificationSort.Delivery] = ([n[1], n[0], n[3], n[2]], [n[2], n[3], n[0], n[1]]),

                // One kind exists, so the column orders by its tie-break alone - which is still not
                // the default newest-first order.
                [NotificationSort.Kind] = ([n[0], n[1], n[2], n[3]], [n[3], n[2], n[1], n[0]]),
                [NotificationSort.Recipient] = ([n[2], n[3], n[0], n[1]], [n[1], n[0], n[3], n[2]]),

                // Sent before Failed: the declaration order, not the alphabet.
                [NotificationSort.Outcome] = ([n[1], n[3], n[0], n[2]], [n[2], n[0], n[3], n[1]]),
                [NotificationSort.AttemptedAt] = ([n[1], n[2], n[0], n[3]], [n[3], n[0], n[2], n[1]]),
            },
            skipped: [],
            query => DeliveryListTests.ReadIdsAsync(client, admin, "/api/notifications" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_of_the_client_roster_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        var c = new int[4];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            (string First, string Last, string Email, string Phone)[] rows =
            [
                ("Б", "В", "b@drivetrack.test", "+380440000004"),
                ("Г", "А", "c@drivetrack.test", "+380440000002"),
                ("А", "Г", "d@drivetrack.test", "+380440000001"),
                ("В", "Б", "a@drivetrack.test", "+380440000003"),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var seeded = await Seed.ClientAsync(context, cancellationToken);
                var user = await context.Users.FindAsync([seeded.UserId.Value], cancellationToken);

                user!.FirstName = rows[index].First;
                user.LastName = rows[index].Last;
                user.Email = rows[index].Email;
                seeded.PhoneNumber = rows[index].Phone;

                await context.SaveChangesAsync(cancellationToken);

                c[index] = user.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<ClientSort, (int[] Ascending, int[] Descending)>
            {
                [ClientSort.FirstName] = ([c[2], c[0], c[3], c[1]], [c[1], c[3], c[0], c[2]]),
                [ClientSort.Email] = ([c[3], c[0], c[1], c[2]], [c[2], c[1], c[0], c[3]]),
                [ClientSort.PhoneNumber] = ([c[2], c[1], c[3], c[0]], [c[0], c[3], c[1], c[2]]),
            },

            // Surname order is the roster's default, so it has no default order to differ from; it
            // is asserted on its own below.
            skipped: [ClientSort.LastName],
            query => DeliveryListTests.ReadIdsAsync(client, admin, "/api/clients" + query, cancellationToken, "userId"));

        Assert.Equal(
            [c[1], c[3], c[0], c[2]],
            await DeliveryListTests.ReadIdsAsync(client, admin, "/api/clients?sort=lastName", cancellationToken, "userId"));
        Assert.Equal(
            [c[2], c[0], c[3], c[1]],
            await DeliveryListTests.ReadIdsAsync(client, admin, "/api/clients?sort=lastName&desc=true", cancellationToken, "userId"));
    }

    [Fact]
    public async Task Every_column_of_the_dispatcher_roster_reorders_the_rows_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        var p = new int[3];

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var role = await context.Roles.SingleAsync(candidate => candidate.Name == nameof(UserRole.Dispatcher), cancellationToken);

            (string First, string Last, string Email)[] rows =
            [
                ("А", "Б", "c@drivetrack.test"),
                ("Б", "В", "a@drivetrack.test"),
                ("В", "А", "b@drivetrack.test"),
            ];

            for (var index = 0; index < rows.Length; index++)
            {
                var user = await Seed.UserAsync(context, cancellationToken);

                user.FirstName = rows[index].First;
                user.LastName = rows[index].Last;
                user.Email = rows[index].Email;
                context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });

                await context.SaveChangesAsync(cancellationToken);

                p[index] = user.Id;
            }
        }

        await DeliveryListTests.AssertEveryColumnReordersAsync(
            new Dictionary<DispatcherSort, (int[] Ascending, int[] Descending)>
            {
                [DispatcherSort.FirstName] = ([p[0], p[1], p[2]], [p[2], p[1], p[0]]),
                [DispatcherSort.Email] = ([p[1], p[2], p[0]], [p[0], p[2], p[1]]),
            },

            // Surname order is the roster's default; asserted on its own below.
            skipped: [DispatcherSort.LastName],
            query => DeliveryListTests.ReadIdsAsync(client, admin, "/api/dispatchers" + query, cancellationToken, "userId"));

        Assert.Equal(
            [p[2], p[0], p[1]],
            await DeliveryListTests.ReadIdsAsync(client, admin, "/api/dispatchers?sort=lastName", cancellationToken, "userId"));
        Assert.Equal(
            [p[1], p[0], p[2]],
            await DeliveryListTests.ReadIdsAsync(client, admin, "/api/dispatchers?sort=lastName&desc=true", cancellationToken, "userId"));
    }

    /// <summary>A driver whose account carries the name given, optionally holding a vehicle.</summary>
    private static async Task<DriverId> DriverNamedAsync(
        AppDbContext context,
        string firstName,
        string lastName,
        CancellationToken cancellationToken,
        int? vehicleId = null,
        string? licence = null)
    {
        var driver = await Seed.DriverAsync(context, cancellationToken, vehicleId);

        if (licence is not null)
        {
            driver.LicenseNumber = licence;
        }

        var user = await context.Users.FindAsync([driver.UserId.Value], cancellationToken);

        user!.FirstName = firstName;
        user.LastName = lastName;

        await context.SaveChangesAsync(cancellationToken);

        return driver.Id;
    }

    private static async Task<JsonElement> PageAsync(
        HttpClient client,
        string token,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await FleetApi.SendAsync(client, HttpMethod.Get, path, token, body: null, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await FleetApi.DataAsync(response, cancellationToken);
    }

    private static async Task<int> TotalAsync(
        HttpClient client,
        string token,
        string path,
        CancellationToken cancellationToken) =>
        (await PageAsync(client, token, path, cancellationToken)).GetProperty("total").GetInt32();

    private static async Task<string?[]> LastNamesAsync(
        HttpClient client,
        string token,
        string path,
        CancellationToken cancellationToken) =>
        [
            .. (await PageAsync(client, token, path, cancellationToken))
                .GetProperty("items")
                .EnumerateArray()
                .Select(row => row.GetProperty("lastName").GetString()),
        ];
}
