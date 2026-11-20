using System.Globalization;
using System.Text.RegularExpressions;
using DriveTrack.Application.Accounts.Clients;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Common.Locations;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Deliveries;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Vehicles;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Drivers;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Components.Failures;
using DriveTrack.Web.Components.Pages.Deliveries;
using DriveTrack.Web.Components.Shared.DesignSystem;
using DriveTrack.Web.Components.Shared.Display;
using DriveTrack.Web.Components.Shared.Screens;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using DeliveryFilter = DriveTrack.Web.Components.Pages.Deliveries.DeliveriesPage.DeliveryFilter;
using DeliveryForm = DriveTrack.Web.Components.Pages.Deliveries.DeliveriesPage.DeliveryForm;
using RequestForm = DriveTrack.Web.Components.Pages.Deliveries.MyDeliveriesPage.RequestForm;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>The dispatch board: filters, sortable headings, the window cell and the address it asks with.</summary>
public partial class DeliveryScreenTests
{
    [Fact]
    public async Task The_dispatch_board_offers_every_filter_and_a_single_reset()
    {
        // A filter per column, a creation-date range, and one action that clears
        // all of them. A per-field clear would leave a dispatcher hunting for the box still
        // narrowing the list.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        foreach (var id in new[]
                 {
                     "delivery-filter-driver",
                     "delivery-filter-client",
                     "delivery-filter-pickup",
                     "delivery-filter-dropoff",
                     "delivery-filter-details",
                     "delivery-filter-notes",
                     "delivery-filter-status",
                     "delivery-filter-created-from",
                     "delivery-filter-created-to",
                     "delivery-filter-overdue",
                 })
        {
            Assert.Contains($@"id=""{id}""", html, StringComparison.Ordinal);
        }

        Assert.Contains("Скинути фільтри", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_board_says_so_and_offers_nothing_there_is_no_way_out_of()
    {
        // The state nothing asserted, on a screen whose whole job is a list. An empty board has two
        // causes - filters that match none of the fetched rows, and a fetch that came back with
        // nothing - and only the first has a way out. Static rendering cannot type into a filter, so
        // what is reachable here is the second: a board with no deliveries at all, which must say it
        // is empty and must not offer to clear filters nobody set.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher, board: []);

        Assert.Contains("dt-table-empty", html, StringComparison.Ordinal);
        Assert.Contains("Ще немає жодної доставки.", html, StringComparison.Ordinal);

        // By its own hook, not by its words: the filter panel above carries a reset that says
        // exactly the same thing, so a text assertion here would be green whatever the empty state
        // did.
        Assert.DoesNotContain("dt-delivery-empty-reset", html, StringComparison.Ordinal);

        // And the panel's own reset is still there, so the assertion above is about the empty state
        // rather than about a screen that lost both.
        Assert.Contains("dt-delivery-filters-reset", html, StringComparison.Ordinal);

        // No rows, and none of the chrome that only a row has: the truncation note is a claim about
        // a full page, and an empty board is the opposite of one.
        Assert.Equal(0, SharedMarkup.Occurrences(html, "dt-table-row"));
        Assert.DoesNotContain("dt-table-note", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_filters_are_one_named_group_and_the_create_action_sits_with_the_heading()
    {
        // The restructure's own claims, which the id list above cannot see: it passed on the three
        // loose rows of inputs this replaced, and would pass again on a silent revert to them.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        // A real <form> with an accessible name, so the ten controls are announced as the filters
        // they are rather than as ten unrelated boxes between the heading and the table. The name is
        // read back in the language a user gets it in: a key that resolved to itself would be Latin
        // here and nowhere else.
        var panel = FilterPanel.Match(html);

        Assert.True(panel.Success, "The filters are not a form carrying the card surface.");
        Assert.Contains(@"aria-label=""Фільтри""", panel.Value, StringComparison.Ordinal);

        // Every one of the ten is inside it, including the reset - a panel that named itself and
        // then held half the controls would be worse than none.
        var group = html[panel.Index..html.IndexOf("</form>", panel.Index, StringComparison.Ordinal)];

        Assert.Contains(@"id=""delivery-filter-driver""", group, StringComparison.Ordinal);
        Assert.Contains(@"id=""delivery-filter-overdue""", group, StringComparison.Ordinal);
        Assert.Contains("dt-delivery-filters-reset", group, StringComparison.Ordinal);

        // And the create action is beside the heading rather than in a row of its own: it is about
        // the collection the heading names, and the reset - which narrows that collection - is not.
        var head = PageHead.Match(html);

        Assert.True(head.Success, "The screen has no heading row.");
        Assert.Contains("dt-delivery-create", head.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("<h1", head.Groups["body"].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-delivery-filters-reset", head.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_actions_column_keeps_its_name_without_printing_it()
    {
        // The buttons under it say what they do, so a column name printed above them competes with
        // them for the same width - and a column with no name at all leaves a screen reader moving
        // by cell with one unlabelled column. Hidden, not dropped.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        var actions = ActionsHeader.Match(html);

        Assert.True(actions.Success, "The table has no actions heading.");
        Assert.Contains("dt-visually-hidden", actions.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("Дії", actions.Groups["body"].Value, StringComparison.Ordinal);

        // Not a sort control: it names no value to order by, and counting it as one would put the
        // sortable count at ten.
        Assert.DoesNotContain("<button", actions.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_column_heading_is_a_sort_control()
    {
        // DtDataTable renders whatever rows it is given and sorts nothing, so the control belongs
        // to the consumer - and a heading that was only a label would leave sorting unimplemented
        // while the table looked complete.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        var sortable = HeaderCell.Matches(html)
            .Count(cell => cell.Groups["body"].Value.Contains("<button", StringComparison.Ordinal));

        // Nine sortable columns and the actions column, which is not one.
        Assert.Equal(9, sortable);

        // And the reordering is perceivable: without aria-sort the rows rearrange silently, and a
        // screen-reader user is given no way to tell which column did it. The board opens ordered
        // by the delivery window, so exactly one heading claims a direction and the rest claim none.
        Assert.Equal(1, SharedMarkup.Occurrences(html, @"aria-sort=""ascending"""));
        Assert.Equal(8, SharedMarkup.Occurrences(html, @"aria-sort=""none"""));

        // The sighted half of the same fact, which aria-sort alone leaves to a colour. Eight
        // headings wear the stacked pair that means "this sorts"; the sorted one wears a single
        // chevron pointing the way the rows went, so ascending and descending are told apart by
        // shape rather than by hue. Asserted on the drawn path rather than on the enum member,
        // because what a reader gets is the glyph: a member wired to the wrong path would leave
        // every name in the source right and every heading on the page identical.
        Assert.Equal(8, SharedMarkup.Occurrences(html, SortGlyph));
        Assert.Equal(1, SharedMarkup.Occurrences(html, AscendingGlyph));
        Assert.Equal(0, SharedMarkup.Occurrences(html, DescendingGlyph));

        // And it is the delivery window that claims it. Counting one ascending heading says a
        // column is sorted and not which, so the board could open on any of the nine - the creation
        // date included - and every count above would still hold.
        // The window is the column the design leads with, because a dispatcher's question is what
        // is due next.
        var claiming = SortedHeader.Match(html);

        Assert.True(claiming.Success, "No heading claims a sort direction.");
        Assert.Contains("Часове вікно", claiming.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains(AscendingGlyph, claiming.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(WindowShapes))]
    public async Task The_window_cell_says_which_bounds_it_has(
        DateTimeOffset? earliest,
        DateTimeOffset? latest,
        string expected)
    {
        // A delivery window lets either bound stand alone and lets both be absent, so the cell has
        // four shapes and only one of them is a range. A one-sided window used to render as a range
        // with a hole in it - a dash standing in for the missing bound, beside the dash that means
        // "to" - which says nothing about which end is missing.
        //
        // Read off the rendered cell rather than off the formatter, because the word that makes a
        // single bound directional is in the markup and the figures are in the method: an assertion
        // against either half alone would pass on a cell that never puts the two together.
        var html = await RenderDeliveriesAsync(
            UserRole.Dispatcher,
            board: [Row(windowEarliest: earliest, windowLatest: latest)]);

        var cell = WindowCell.Match(html);

        Assert.True(cell.Success, "The board renders no window cell.");

        // The cell leads with its own name, as the own-deliveries cell does: the label that stands
        // in for the column heading once the head is dropped below the breakpoint, and which is
        // therefore in the markup at every width.
        Assert.Equal("Часове вікно " + expected, Words(cell.Groups["body"].Value));
    }

    /// <summary>
    /// The four shapes a delivery window takes, and what the first column says about each. The
    /// figures are formatted here the way the screen formats them - local time, the reader's own
    /// culture - so the case asserts the shape of the cell rather than restating a date format that
    /// would fail on a machine in another zone.
    /// </summary>
    public static TheoryData<DateTimeOffset?, DateTimeOffset?, string> WindowShapes()
    {
        var morning = Local(2026, 9, 17).AddHours(-3);
        var noon = Local(2026, 9, 17);
        var overnight = Local(2026, 9, 18).AddHours(-1);

        return new TheoryData<DateTimeOffset?, DateTimeOffset?, string>
        {
            // Both bounds inside one local day: the date is said once and the two times bound it.
            { morning, noon, Moment(morning) + " – " + Time(noon) },

            // Both bounds across midnight: each side carries its own date, or the second figure
            // would silently claim the first one's day.
            { noon, overnight, Moment(noon) + " – " + Moment(overnight) },

            // One bound apiece: a direction rather than a range.
            { morning, null, "з " + Moment(morning) },
            { null, noon, "до " + Moment(noon) },

            // None at all: the dash that stands for a value there is none of.
            { null, null, "—" },
        };
    }

    [Fact]
    public async Task The_overdue_mark_rides_in_the_window_cell_rather_than_a_column_of_its_own()
    {
        // Overdue is a badge inside the delivery window rather than a column of its own - the
        // header map above proves there is no such column, but nothing else proves where the mark
        // went, and a badge that survived in the wrong cell would leave both
        // the column count and the catalogue exactly as they should be.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        // Matched on the class as a token rather than on the whole opening tag: the screen carries a
        // scoped stylesheet, so Blazor stamps its own attribute after the class and an exact tag
        // match would find nothing.
        var windows = WindowCell.Matches(html);

        Assert.NotEmpty(windows);

        var marked = windows.Count(cell =>
            cell.Groups["body"].Value.Contains("dt-badge--overdue", StringComparison.Ordinal));

        // Not vacuous - the board this renders has an overdue row.
        Assert.NotEqual(0, marked);

        // And every mark on the page is one of those: an equal count is what rules out a second
        // home for it somewhere else in the row.
        Assert.Equal(marked, SharedMarkup.Occurrences(html, "dt-badge--overdue"));
    }

    [Fact]
    public void The_sorted_heading_draws_a_different_glyph_for_each_direction()
    {
        // The arm the page cannot show. A board renders fresh and therefore ascending, so the test
        // above can only ever see two of the three glyphs, and a version that answered the same
        // chevron for both directions would render perfectly and satisfy every assertion the markup
        // can make about itself. That is the whole claim of drawing the direction at all, so it is
        // asserted where it is reachable: on the choice every sortable heading in the product makes.
        var ascending = SortHeading.Glyph(sorted: true, descending: false);
        var descending = SortHeading.Glyph(sorted: true, descending: true);

        Assert.NotEqual(ascending, descending);
        Assert.Equal(IconName.SortAscending, ascending);
        Assert.Equal(IconName.SortDescending, descending);

        // And every other column keeps the stacked pair, in both directions: the glyph says which
        // column is sorted as well as which way, and a column that wore a chevron while another
        // held the order would be two headings claiming it.
        Assert.Equal(IconName.Sort, SortHeading.Glyph(sorted: false, descending: false));
        Assert.Equal(IconName.Sort, SortHeading.Glyph(sorted: false, descending: true));

        Assert.Equal("ascending", SortHeading.State(sorted: true, descending: false));
        Assert.Equal("descending", SortHeading.State(sorted: true, descending: true));
        Assert.Equal("none", SortHeading.State(sorted: false, descending: true));
    }

    [Fact]
    public void Each_heading_sorts_the_column_it_is_labelled_with()
    {
        // The join the halves above leave open. Counting the buttons proves every column has a
        // control and the repository's tests prove every column has an ordering, but neither can
        // tell whether the control on the "Driver" heading is wired to the driver column - swap two
        // SortHandler arguments and the screen sorts by the wrong thing while both stay green.
        //
        // Read from source rather than from the render, because the wiring is what is under test:
        // the rendered markup carries no trace of which column a click would sort by.
        var page = SharedMarkup.ReadComponent("Pages", "Deliveries", "DeliveriesPage.razor");

        var expected = new Dictionary<DeliverySort, string>
        {
            // The window is the board's first column and the only one whose key is not its own
            // member name: the catalogue already carried `DeliveryWindow` for the landing page's
            // summary, and a second entry saying the same word in the same two languages would be
            // duplication for nothing.
            [DeliverySort.Window] = "DeliveryWindow",
            [DeliverySort.Driver] = "Driver",
            [DeliverySort.Client] = "Client",
            [DeliverySort.Pickup] = "Pickup",
            [DeliverySort.Dropoff] = "Dropoff",
            [DeliverySort.PackageDetails] = "PackageDetails",
            [DeliverySort.DeliveryNotes] = "DeliveryNotes",
            [DeliverySort.Status] = "Status",
            [DeliverySort.CreatedAt] = "CreatedAt",
        };

        // Every member, so a column added to the enum without a heading is caught here rather than
        // shipping as a sort nobody can reach.
        Assert.Equal(Enum.GetValues<DeliverySort>().Length, expected.Count);

        var wired = new Dictionary<DeliverySort, string>();

        foreach (Match cell in HeaderCell.Matches(page))
        {
            var body = cell.Groups["body"].Value;

            var column = Regex.Match(
                body,
                @"SortHandler\(DeliverySort\.(?<name>[A-Za-z]+)\)",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            if (!column.Success)
            {
                continue;
            }

            var key = Regex.Match(
                body,
                @"Localizer\[""(?<key>[^""]+)""\]",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            Assert.True(key.Success, $"The '{column.Groups["name"].Value}' heading renders no label.");

            wired[Enum.Parse<DeliverySort>(column.Groups["name"].Value)] = key.Groups["key"].Value;
        }

        Assert.Equal(expected, wired);
    }

    [Fact]
    public async Task The_dispatch_board_asks_for_its_opening_view_with_no_filter_set()
    {
        var service = new StubDeliveryService();

        await RenderDeliveriesAsync(UserRole.Dispatcher, service: service);

        var asked = Assert.Single(service.AskedBoard);

        Assert.Equal(new ListDeliveriesQuery(0, TableQuery.DefaultPageSize, nameof(DeliverySort.Window), false), asked);
    }

    [Fact]
    public async Task An_address_carrying_every_filter_asks_for_all_of_them_and_a_sort_keeps_them()
    {
        // The board's half of "a shared link reproduces the view": every filter the panel has is
        // read out of the address into the request - and written back into the address when the
        // reader moves the view on, so a sort does not quietly drop a filter from the link.
        const string address =
            "deliveries?page=2&size=25&sort=Status&desc=true"
            + "&driver=%D0%BA%D0%BE%D0%B2&client=%D0%BF%D0%B5%D1%82&pickup=%D1%85%D1%80%D0%B5%D1%89"
            + "&dropoff=%D1%80%D0%B8%D0%BD%D0%BE%D0%BA&details=%D0%BF%D0%B0%D0%BB%D0%B5%D1%82"
            + "&notes=%D0%B4%D0%B7%D0%B2%D0%BE%D0%BD%D0%B8%D1%82%D0%B8&status=InTransit"
            + "&from=2026-09-01&to=2026-09-30&overdue=true";

        var service = new StubDeliveryService();
        var navigation = new ComponentRenderer.RecordingNavigationManager(address);

        await ComponentRenderer.RenderThenAsync<ProbeBoard>(
            services =>
            {
                services.AddSupplyValueFromQueryProvider();
                services.AddSingleton<NavigationManager>(navigation);
                services.AddSingleton<ICurrentUser>(new StubCaller(UserRole.Dispatcher));
                services.AddSingleton<IDeliveryService>(service);
                services.AddSingleton<IDriverService>(new StubDriverService());
                services.AddSingleton<IVehicleService>(new StubVehicleService());
                services.AddSingleton<IClientAdministrationService>(new StubClientRoster());
            },
            _ => ProbeBoard.Last!.SortAsync(DeliverySort.CreatedAt));

        Assert.Equal(
            new ListDeliveriesQuery(
                25,
                25,
                nameof(DeliverySort.Status),
                true,
                "ков",
                "пет",
                "хрещ",
                "ринок",
                "палет",
                "дзвонити",
                DeliveryStatus.InTransit,
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 30),
                true),
            service.AskedBoard[0]);

        // The sort went back to page one and kept every filter.
        Assert.Equal(
            service.AskedBoard[0] with { Offset = 0, Sort = nameof(DeliverySort.CreatedAt), Descending = false },
            service.AskedBoard[^1]);

        // The stub board holds two rows, so page two of twenty-five falls back to page one first;
        // the sort's address is the last one written.
        var written = Uri.UnescapeDataString(navigation.Visited[^1]);

        foreach (var part in new[]
                 {
                     "size=25", "sort=CreatedAt", "driver=ков", "client=пет", "pickup=хрещ", "dropoff=ринок",
                     "details=палет", "notes=дзвонити", "status=InTransit", "from=2026-09-01", "to=2026-09-30",
                     "overdue=true",
                 })
        {
            Assert.Contains(part, written, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("page=", written, StringComparison.Ordinal);
        Assert.DoesNotContain("desc=", written, StringComparison.Ordinal);
    }
}
