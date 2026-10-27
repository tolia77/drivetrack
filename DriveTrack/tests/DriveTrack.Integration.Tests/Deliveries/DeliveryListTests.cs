using System.Globalization;
using System.Net;
using System.Text.Json;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using DriveTrack.Integration.Tests.Fleet;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Infrastructure.Persistence;

namespace DriveTrack.Integration.Tests.Deliveries;

/// <summary>
/// The delivery lists paged, filtered and sorted by the database, over every row the caller may
/// see - the I/O matrix of the server-side paging story, asserted against PostgreSQL rather than
/// against a page already fetched.
/// <para>
/// Rows are seeded through the context, because what is under test is the WHERE, the ORDER BY and
/// the COUNT, not the create endpoint's throughput. Every read goes through the REST route, so the
/// envelope a screen or a REST caller receives is the one asserted.
/// </para>
/// </summary>
public class DeliveryListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_first_page_carries_ten_rows_and_the_total_of_all_eighty_six()
    {
        // The matrix's first row, and the acceptance criterion behind it: paging to the end shows the
        // last rows in the chosen order, and the range reads "N–M of T".
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        var ids = await SeedAsync(factory, 86, cancellationToken);

        var first = await PageAsync(client, dispatcher, "?limit=10", cancellationToken);

        Assert.Equal(86, first.GetProperty("total").GetInt32());
        Assert.Equal(0, first.GetProperty("offset").GetInt32());
        Assert.Equal(10, first.GetProperty("limit").GetInt32());

        // Newest first with no column named: the ten highest ids.
        Assert.Equal(ids.OrderDescending().Take(10), Ids(first));

        // The last page holds the remaining six - the oldest rows, which are reachable now.
        var last = await PageAsync(client, dispatcher, "?limit=10&offset=80", cancellationToken);

        Assert.Equal(86, last.GetProperty("total").GetInt32());
        Assert.Equal(ids.Order().Take(6).OrderDescending(), Ids(last));

        // And the pages tile the list: nothing repeated, nothing skipped.
        var seen = new List<int>();

        for (var offset = 0; offset < 86; offset += 10)
        {
            seen.AddRange(Ids(await PageAsync(client, dispatcher, $"?limit=10&offset={offset}", cancellationToken)));
        }

        Assert.Equal(ids.OrderDescending(), seen);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_keeps_the_true_total()
    {
        // What a screen whose last row was just deleted reads back: no rows, and a total that lets it
        // step back to the last page that still exists.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        await SeedAsync(factory, 11, cancellationToken);

        var beyond = await PageAsync(client, dispatcher, "?limit=10&offset=20", cancellationToken);

        Assert.Empty(Ids(beyond));
        Assert.Equal(11, beyond.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_driver_filter_matches_across_every_row_whatever_case_it_is_typed_in()
    {
        // The matrix's third row. The matching rows are the oldest ones - past the first page of a
        // hundred - which is exactly where the old browser-side filter could not reach.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        int[] koval;

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var kovalenko = await DriverNamedAsync(context, "Олег", "Коваленко", cancellationToken);
            var shevchenko = await DriverNamedAsync(context, "Тарас", "Шевченко", cancellationToken);

            var theirs = Enumerable.Range(0, 7).Select(_ => Seed.NewDelivery(driverId: kovalenko)).ToArray();

            context.Deliveries.AddRange(theirs);
            await context.SaveChangesAsync(cancellationToken);

            koval = [.. theirs.Select(delivery => delivery.Id)];

            context.Deliveries.AddRange(Enumerable.Range(0, 110).Select(_ => Seed.NewDelivery(driverId: shevchenko)));
            context.Deliveries.AddRange(Enumerable.Range(0, 5).Select(_ => Seed.NewDelivery()));
            await context.SaveChangesAsync(cancellationToken);
        }

        var page = await PageAsync(client, dispatcher, "?limit=5&driver=" + Uri.EscapeDataString("КОВАЛ"), cancellationToken);

        Assert.Equal(7, page.GetProperty("total").GetInt32());
        Assert.Equal(koval.OrderDescending().Take(5), Ids(page));

        Assert.All(
            page.GetProperty("items").EnumerateArray(),
            row => Assert.Contains("Коваленко", row.GetProperty("driver").GetProperty("name").GetString()!, StringComparison.Ordinal));

        // An unassigned delivery has no name to match, so a driver filter narrows it away.
        var nobody = await PageAsync(client, dispatcher, "?driver=" + Uri.EscapeDataString("о"), cancellationToken);

        Assert.Equal(117, nobody.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_text_filter_takes_the_wildcards_it_is_given_literally()
    {
        // ILIKE's own wildcards, escaped: a dispatcher typing "50%" means fifty percent, and an
        // underscore is an underscore rather than "any one character".
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var literal = Seed.NewDelivery();
            literal.PackageDetails = "Знижка 50% на коробки";

            var lookalike = Seed.NewDelivery();
            lookalike.PackageDetails = "Знижка 500 на коробки";

            var underscored = Seed.NewDelivery();
            underscored.DeliveryNotes = "код_А";

            var plain = Seed.NewDelivery();
            plain.DeliveryNotes = "кодXА";

            context.Deliveries.AddRange(literal, lookalike, underscored, plain);
            await context.SaveChangesAsync(cancellationToken);
        }

        var percent = await PageAsync(client, dispatcher, "?packageDetails=" + Uri.EscapeDataString("50%"), cancellationToken);
        var underscore = await PageAsync(client, dispatcher, "?deliveryNotes=" + Uri.EscapeDataString("д_А"), cancellationToken);

        Assert.Equal(1, percent.GetProperty("total").GetInt32());
        Assert.Equal(1, underscore.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_location_filters_read_the_address_or_the_coordinates_the_cell_shows()
    {
        // FR-94: a place with no resolved address is shown, and therefore searched, by its
        // coordinates.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var addressed = Seed.NewDelivery();
            addressed.DropoffLocation = new global::DriveTrack.Domain.Common.Location(49.8397, 24.0297, "Львів, площа Ринок", Seed.Instant);

            // Stored at six decimals, shown at five: the cell reads 50.45013, rounded up.
            var precise = Seed.NewDelivery();
            precise.PickupLocation = Seed.Location(50.450126, 30.523449);

            context.Deliveries.AddRange(addressed, Seed.NewDelivery(), precise);
            await context.SaveChangesAsync(cancellationToken);
        }

        var byAddress = await PageAsync(client, dispatcher, "?dropoff=" + Uri.EscapeDataString("ринок"), cancellationToken);
        var byCoordinates = await PageAsync(client, dispatcher, "?pickup=" + Uri.EscapeDataString("50.4501"), cancellationToken);

        Assert.Equal(1, byAddress.GetProperty("total").GetInt32());
        Assert.Equal(3, byCoordinates.GetProperty("total").GetInt32());

        // Typing exactly what the cell shows finds the row; the stored precision is not on screen.
        Assert.Equal(
            "50.45013; 30.52345",
            Web.Components.Pages.LocationText.Describe(new LocationView(new MapLocation(50.450126, 30.523449), null)));

        var asShown = await PageAsync(client, dispatcher, "?pickup=" + Uri.EscapeDataString("50.45013; 30.52345"), cancellationToken);
        var asStored = await PageAsync(client, dispatcher, "?pickup=" + Uri.EscapeDataString("50.450126"), cancellationToken);

        Assert.Equal(1, asShown.GetProperty("total").GetInt32());
        Assert.Equal(0, asStored.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_status_the_overdue_switch_and_the_creation_days_narrow_together()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        int overdueId;

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Overdue: a window that closed long ago, still in transit.
            var overdue = Seed.NewDelivery(
                windowLatestAt: DateTimeOffset.UtcNow.AddDays(-2),
                status: DeliveryStatus.InTransit);

            // The same window, but delivered: not overdue, whatever the clock says.
            var done = Seed.NewDelivery(
                windowLatestAt: DateTimeOffset.UtcNow.AddDays(-2),
                status: DeliveryStatus.Delivered);

            var pending = Seed.NewDelivery(windowLatestAt: DateTimeOffset.UtcNow.AddDays(3));

            context.Deliveries.AddRange(overdue, done, pending);
            await context.SaveChangesAsync(cancellationToken);

            overdueId = overdue.Id;
        }

        var late = await PageAsync(client, dispatcher, "?overdue=true", cancellationToken);
        var delivered = await PageAsync(client, dispatcher, "?status=Delivered", cancellationToken);
        var lateAndDelivered = await PageAsync(client, dispatcher, "?status=Delivered&overdue=true", cancellationToken);

        Assert.Equal([overdueId], Ids(late));
        Assert.Equal(1, delivered.GetProperty("total").GetInt32());
        Assert.Equal(0, lateAndDelivered.GetProperty("total").GetInt32());

        // The creation days are local days and both ends are inclusive: every seeded row was opened
        // on Seed.Instant's local day.
        var day = DateOnly.FromDateTime(Seed.Instant.ToLocalTime().DateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var next = DateOnly.FromDateTime(Seed.Instant.ToLocalTime().DateTime).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var thatDay = await PageAsync(client, dispatcher, $"?createdFrom={day}&createdTo={day}", cancellationToken);
        var after = await PageAsync(client, dispatcher, $"?createdFrom={next}", cancellationToken);

        Assert.Equal(3, thatDay.GetProperty("total").GetInt32());
        Assert.Equal(0, after.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("?createdTo=9999-12-31")]
    [InlineData("?createdFrom=0001-01-01")]
    [InlineData("?createdFrom=0001-01-01&createdTo=9999-12-31")]
    public async Task A_creation_day_at_the_edge_of_the_calendar_bounds_nothing_rather_than_failing(string query)
    {
        // The day after 9999-12-31 does not exist, and local midnight on 0001-01-01 under a zone
        // ahead of UTC is before the first instant a DateTimeOffset can hold. Either is a bound
        // that bounds nothing - every delivery is inside it - never a 500.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        await SeedAsync(factory, 3, cancellationToken);

        var page = await PageAsync(client, dispatcher, query, cancellationToken);

        Assert.Equal(3, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_status_column_sorts_by_the_lifecycle_and_reverses_on_the_second_press()
    {
        // The matrix's fourth row: sorted by status then id, and descending reverses both - so the
        // tie-break follows the direction and pages never repeat a row.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        var seeded = new List<(int Id, DeliveryStatus Status)>();

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            foreach (var status in new[]
                     {
                         DeliveryStatus.Delivered, DeliveryStatus.Pending, DeliveryStatus.Failed,
                         DeliveryStatus.InTransit, DeliveryStatus.Pending, DeliveryStatus.Delivered,
                     })
            {
                var delivery = Seed.NewDelivery(status: status);

                context.Deliveries.Add(delivery);
                await context.SaveChangesAsync(cancellationToken);

                seeded.Add((delivery.Id, status));
            }
        }

        var ascending = await PageAsync(client, dispatcher, "?sort=status", cancellationToken);
        var descending = await PageAsync(client, dispatcher, "?sort=Status&desc=true", cancellationToken);

        var expected = seeded.OrderBy(row => (int)row.Status).ThenBy(row => row.Id).Select(row => row.Id).ToArray();

        Assert.Equal(expected, Ids(ascending));
        Assert.Equal(expected.Reverse(), Ids(descending));

        // Pending before In transit before Delivered before Failed - not alphabetical.
        Assert.Equal(
            nameof(DeliveryStatus.Pending),
            ascending.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_window_column_keeps_deliveries_with_no_window_at_the_far_end_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        var day = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

        int both, none, onlyLatest, onlyEarliest;

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var a = Seed.NewDelivery(windowEarliestAt: day.AddDays(2), windowLatestAt: day.AddDays(3));
            var b = Seed.NewDelivery();
            var c = Seed.NewDelivery(windowLatestAt: day);
            var d = Seed.NewDelivery(windowEarliestAt: day.AddDays(1));

            context.Deliveries.AddRange(a, b, c, d);
            await context.SaveChangesAsync(cancellationToken);

            (both, none, onlyLatest, onlyEarliest) = (a.Id, b.Id, c.Id, d.Id);
        }

        var ascending = await PageAsync(client, dispatcher, "?sort=window", cancellationToken);
        var descending = await PageAsync(client, dispatcher, "?sort=window&desc=true", cancellationToken);

        // Due first by the near bound, or the far one when that is all there is.
        Assert.Equal([onlyLatest, onlyEarliest, both, none], Ids(ascending));
        Assert.Equal([both, onlyEarliest, onlyLatest, none], Ids(descending));
    }

    [Fact]
    public async Task Every_column_of_the_dispatch_board_reorders_the_rows_both_ways()
    {
        // A sort arm that went missing falls through to the default order without a sound, so every
        // member of the enum is walked over four rows whose order differs column by column - and
        // from the default, newest first, in both directions.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        var r = await SeedWalkAsync(factory, owner: null, cancellationToken);

        var expected = new Dictionary<DeliverySort, (int[] Ascending, int[] Descending)>
        {
            [DeliverySort.Window] = ([r[2], r[3], r[0], r[1]], [r[0], r[3], r[2], r[1]]),
            [DeliverySort.Driver] = ([r[2], r[3], r[0], r[1]], [r[1], r[0], r[3], r[2]]),
            [DeliverySort.Client] = ([r[0], r[1], r[3], r[2]], [r[2], r[3], r[1], r[0]]),
            [DeliverySort.Pickup] = ([r[3], r[2], r[0], r[1]], [r[1], r[0], r[2], r[3]]),
            [DeliverySort.Dropoff] = ([r[1], r[2], r[0], r[3]], [r[3], r[0], r[2], r[1]]),
            [DeliverySort.PackageDetails] = ([r[3], r[1], r[2], r[0]], [r[0], r[2], r[1], r[3]]),
            [DeliverySort.DeliveryNotes] = ([r[1], r[0], r[3], r[2]], [r[2], r[3], r[0], r[1]]),
            [DeliverySort.Status] = ([r[1], r[3], r[0], r[2]], [r[2], r[0], r[3], r[1]]),
            [DeliverySort.CreatedAt] = ([r[1], r[3], r[0], r[2]], [r[2], r[0], r[3], r[1]]),
        };

        await AssertEveryColumnReordersAsync(
            expected,
            skipped: [],
            query => ReadIdsAsync(client, dispatcher, "/api/deliveries" + query, cancellationToken));
    }

    [Fact]
    public async Task Every_column_the_own_list_offers_reorders_the_rows_both_ways()
    {
        // The same walk over one client's own rows. The two name columns are refused on this list
        // (AD-17) - asserted by The_own_list_refuses_a_column_it_does_not_have - and skipped here.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);
        var requester = await DeliveryApi.ClientAsync(client, dispatcher, cancellationToken);

        var r = await SeedWalkAsync(factory, new ClientId(requester.ClientId), cancellationToken);

        var expected = new Dictionary<DeliverySort, (int[] Ascending, int[] Descending)>
        {
            [DeliverySort.Window] = ([r[2], r[3], r[0], r[1]], [r[0], r[3], r[2], r[1]]),
            [DeliverySort.Pickup] = ([r[3], r[2], r[0], r[1]], [r[1], r[0], r[2], r[3]]),
            [DeliverySort.Dropoff] = ([r[1], r[2], r[0], r[3]], [r[3], r[0], r[2], r[1]]),
            [DeliverySort.PackageDetails] = ([r[3], r[1], r[2], r[0]], [r[0], r[2], r[1], r[3]]),
            [DeliverySort.DeliveryNotes] = ([r[1], r[0], r[3], r[2]], [r[2], r[3], r[0], r[1]]),
            [DeliverySort.Status] = ([r[1], r[3], r[0], r[2]], [r[2], r[0], r[3], r[1]]),
            [DeliverySort.CreatedAt] = ([r[1], r[3], r[0], r[2]], [r[2], r[0], r[3], r[1]]),
        };

        await AssertEveryColumnReordersAsync(
            expected,
            skipped: [DeliverySort.Driver, DeliverySort.Client],
            query => ReadIdsAsync(client, requester.Token, "/api/deliveries/mine" + query, cancellationToken));
    }

    [Fact]
    public async Task The_driver_column_sorts_by_the_name_the_row_shows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var yaryna = await DriverNamedAsync(context, "Ярина", "Бойко", cancellationToken);
            var andrii = await DriverNamedAsync(context, "Андрій", "Мельник", cancellationToken);

            context.Deliveries.AddRange(
                Seed.NewDelivery(driverId: yaryna),
                Seed.NewDelivery(driverId: andrii),
                Seed.NewDelivery());
            await context.SaveChangesAsync(cancellationToken);
        }

        var page = await PageAsync(client, dispatcher, "?sort=driver", cancellationToken);

        var names = page.GetProperty("items")
            .EnumerateArray()
            .Select(row => row.GetProperty("driver").ValueKind == JsonValueKind.Null
                ? string.Empty
                : row.GetProperty("driver").GetProperty("name").GetString())
            .ToArray();

        // Unassigned sorts as the empty name, at the start ascending.
        Assert.Equal([string.Empty, "Андрій Мельник", "Ярина Бойко"], names);
    }

    [Fact]
    public async Task A_client_sees_a_total_of_their_own_rows_and_never_another_client_s()
    {
        // The matrix's scope row: 25 of 300, and AD-3's narrowing comes before every filter, every
        // sort and the count itself.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);
        var requester = await DeliveryApi.ClientAsync(client, dispatcher, cancellationToken);

        await using (var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var other = await Seed.ClientAsync(context, cancellationToken);

            context.Deliveries.AddRange(
                Enumerable.Range(0, 25).Select(_ => Seed.NewDelivery(clientId: new ClientId(requester.ClientId))));
            context.Deliveries.AddRange(Enumerable.Range(0, 275).Select(_ => Seed.NewDelivery(clientId: other.Id)));
            await context.SaveChangesAsync(cancellationToken);
        }

        var mine = await PageAsync(client, requester.Token, "?limit=10&offset=20", cancellationToken, mine: true);

        Assert.Equal(25, mine.GetProperty("total").GetInt32());
        Assert.Equal(5, Ids(mine).Length);

        var sorted = await PageAsync(client, requester.Token, "?sort=status&limit=100", cancellationToken, mine: true);

        Assert.Equal(25, sorted.GetProperty("total").GetInt32());
        Assert.Equal(25, Ids(sorted).Length);
    }

    [Theory]
    [InlineData("?sort=driver")]
    [InlineData("?sort=client")]
    [InlineData("?sort=bogus")]
    public async Task The_own_list_refuses_a_column_it_does_not_have(string query)
    {
        // AD-17: the own-deliveries list carries no counterparty, and ordering by one is a way of
        // reading it - so the two name columns are refused like any column the list lacks.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var dispatcher = await DeliveryApi.DispatcherAsync(factory, client, cancellationToken);
        var requester = await DeliveryApi.ClientAsync(client, dispatcher, cancellationToken);

        using var refused = await FleetApi.SendAsync(
            client, HttpMethod.Get, "/api/deliveries/mine" + query, requester.Token, body: null, cancellationToken);

        await AssertSortRefusedAsync(refused, cancellationToken);
    }

    [Theory]
    [InlineData("/api/deliveries?sort=bogus")]
    [InlineData("/api/deliveries?sort=3")]
    [InlineData("/api/vehicles?sort=bogus")]
    [InlineData("/api/drivers?sort=bogus")]
    [InlineData("/api/shifts?sort=bogus")]
    [InlineData("/api/reviews?sort=bogus")]
    [InlineData("/api/clients?sort=bogus")]
    public async Task An_unknown_sort_is_a_422_naming_the_sort_and_no_rows(string path)
    {
        // The matrix's bad-input row, on every list dispatch can read.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        using var refused = await FleetApi.SendAsync(client, HttpMethod.Get, path, admin, body: null, cancellationToken);

        await AssertSortRefusedAsync(refused, cancellationToken);
    }

    [Theory]
    [InlineData("/api/deliveries?limit=500")]
    [InlineData("/api/notifications?limit=500")]
    [InlineData("/api/dispatchers?limit=500")]
    public async Task A_limit_past_the_ceiling_is_still_a_422_with_the_paging_code(string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await FleetApi.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await FleetApi.TokenAsync(factory, client, UserRole.Admin, cancellationToken);

        using var refused = await FleetApi.SendAsync(client, HttpMethod.Get, path, admin, body: null, cancellationToken);

        await FleetApi.AssertFailureAsync(
            refused, HttpStatusCode.UnprocessableEntity, ErrorCode.COMMON_VALIDATION_FAILED, cancellationToken);

        var fields = (await FleetApi.ReadAsync(refused, cancellationToken)).GetProperty("error").GetProperty("fields");

        Assert.True(fields.TryGetProperty("limit", out _), "The refusal does not name the limit: " + fields);
    }

    private static async Task AssertSortRefusedAsync(HttpResponseMessage refused, CancellationToken cancellationToken)
    {
        await FleetApi.AssertFailureAsync(
            refused, HttpStatusCode.UnprocessableEntity, ErrorCode.COMMON_VALIDATION_FAILED, cancellationToken);

        var envelope = await FleetApi.ReadAsync(refused, cancellationToken);

        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("data").ValueKind);
        Assert.True(
            envelope.GetProperty("error").GetProperty("fields").TryGetProperty("sort", out _),
            "The refusal does not name the sort: " + envelope);
    }

    /// <summary>
    /// Four deliveries whose order differs on every column, in insertion (id) order. With an owner,
    /// every row is that client's and none has a driver or a client name to differ by.
    /// </summary>
    private static async Task<int[]> SeedWalkAsync(
        ApiFactory factory,
        ClientId? owner,
        CancellationToken cancellationToken)
    {
        await using var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken);

        var day = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

        DriverId?[] drivers = owner is null
            ? [
                await DriverNamedAsync(context, "Богдан", "Б", cancellationToken),
                await DriverNamedAsync(context, "Віра", "В", cancellationToken),
                null,
                await DriverNamedAsync(context, "Анна", "А", cancellationToken),
            ]
            : [null, null, null, null];

        ClientId?[] clients = owner is { } own
            ? [own, own, own, own]
            : [
                null,
                await ClientNamedAsync(context, "Анна", cancellationToken),
                await ClientNamedAsync(context, "Віра", cancellationToken),
                await ClientNamedAsync(context, "Богдан", cancellationToken),
            ];

        (DateTimeOffset? Earliest, DateTimeOffset? Latest, string? Pickup, string Dropoff, string Details, string? Notes, DeliveryStatus Status, int Hours)[] rows =
        [
            (day.AddDays(3), null, "Б", "В", "Г", "А", DeliveryStatus.Delivered, 2),
            (null, null, "В", "А", "Б", null, DeliveryStatus.Pending, 0),
            (null, day.AddDays(1), "А", "Б", "В", "В", DeliveryStatus.Failed, 3),
            (day.AddDays(2), null, null, "Г", "А", "Б", DeliveryStatus.InTransit, 1),
        ];

        var ids = new int[rows.Length];

        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var delivery = Seed.NewDelivery(
                clientId: clients[index],
                driverId: drivers[index],
                windowEarliestAt: row.Earliest,
                windowLatestAt: row.Latest,
                status: row.Status);

            delivery.PickupLocation = new global::DriveTrack.Domain.Common.Location(50.4501, 30.5234, row.Pickup, null);
            delivery.DropoffLocation = new global::DriveTrack.Domain.Common.Location(49.8397, 24.0297, row.Dropoff, null);
            delivery.PackageDetails = row.Details;
            delivery.DeliveryNotes = row.Notes;
            delivery.CreatedAt = Seed.Instant.AddHours(row.Hours);

            context.Deliveries.Add(delivery);
            await context.SaveChangesAsync(cancellationToken);

            ids[index] = delivery.Id;
        }

        return ids;
    }

    /// <summary>
    /// Walks every column of <typeparamref name="TColumn"/> but the skipped ones, in both
    /// directions, and asserts each one orders the rows as expected - and differently from the
    /// list's own default order in that direction, which is where a missing sort arm would land.
    /// </summary>
    internal static async Task AssertEveryColumnReordersAsync<TColumn>(
        IReadOnlyDictionary<TColumn, (int[] Ascending, int[] Descending)> expected,
        IReadOnlyCollection<TColumn> skipped,
        Func<string, Task<int[]>> read)
        where TColumn : struct, Enum
    {
        Assert.Equal(
            Enum.GetValues<TColumn>().Except(skipped).Order(),
            expected.Keys.Order());

        var byDefault = await read("?limit=100");
        var byDefaultReversed = await read("?limit=100&desc=true");

        foreach (var (column, order) in expected)
        {
            var ascending = await read($"?limit=100&sort={column}");
            var descending = await read($"?limit=100&sort={column}&desc=true");

            Assert.True(order.Ascending.SequenceEqual(ascending), $"{column} ascending read [{string.Join(", ", ascending)}].");
            Assert.True(order.Descending.SequenceEqual(descending), $"{column} descending read [{string.Join(", ", descending)}].");

            Assert.False(byDefault.SequenceEqual(ascending), $"{column} ascending is indistinguishable from the default order.");
            Assert.False(byDefaultReversed.SequenceEqual(descending), $"{column} descending is indistinguishable from the default order reversed.");
        }
    }

    /// <summary>The ids of one page of a list route, in the order it answered them.</summary>
    internal static async Task<int[]> ReadIdsAsync(
        HttpClient client,
        string token,
        string path,
        CancellationToken cancellationToken,
        string idProperty = "id")
    {
        using var response = await FleetApi.SendAsync(client, HttpMethod.Get, path, token, body: null, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return
        [
            .. (await FleetApi.ItemsAsync(response, cancellationToken))
                .EnumerateArray()
                .Select(row => row.GetProperty(idProperty).GetInt32()),
        ];
    }

    /// <summary>A client whose account carries the first name given.</summary>
    private static async Task<ClientId> ClientNamedAsync(
        AppDbContext context,
        string firstName,
        CancellationToken cancellationToken)
    {
        var seeded = await Seed.ClientAsync(context, cancellationToken);

        var user = await context.Users.FindAsync([seeded.UserId.Value], cancellationToken);

        user!.FirstName = firstName;
        user.LastName = "К";

        await context.SaveChangesAsync(cancellationToken);

        return seeded.Id;
    }

    /// <summary>Seeds unassigned deliveries and answers their ids.</summary>
    private static async Task<int[]> SeedAsync(ApiFactory factory, int count, CancellationToken cancellationToken)
    {
        await using var context = await factory.Database.ContextFactory.CreateDbContextAsync(cancellationToken);

        var deliveries = Enumerable.Range(0, count).Select(_ => Seed.NewDelivery()).ToArray();

        context.Deliveries.AddRange(deliveries);
        await context.SaveChangesAsync(cancellationToken);

        return [.. deliveries.Select(delivery => delivery.Id)];
    }

    /// <summary>A driver whose account carries the name given.</summary>
    private static async Task<DriverId> DriverNamedAsync(
        AppDbContext context,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var driver = await Seed.DriverAsync(context, cancellationToken);

        var user = await context.Users.FindAsync([driver.UserId.Value], cancellationToken);

        user!.FirstName = firstName;
        user.LastName = lastName;

        await context.SaveChangesAsync(cancellationToken);

        return driver.Id;
    }

    private static async Task<JsonElement> PageAsync(
        HttpClient client,
        string token,
        string query,
        CancellationToken cancellationToken,
        bool mine = false)
    {
        using var response = await FleetApi.SendAsync(
            client,
            HttpMethod.Get,
            (mine ? "/api/deliveries/mine" : "/api/deliveries") + query,
            token,
            body: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await FleetApi.DataAsync(response, cancellationToken);
    }

    private static int[] Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetInt32())];
}
