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

/// <summary>The own-deliveries screen drivers and clients see.</summary>
public partial class DeliveryScreenTests
{
    [Fact]
    public async Task The_own_deliveries_screen_names_no_counterparty_and_says_so_when_empty()
    {
        // Hidden counterparties at the component tier. The type this screen renders has no party
        // field at all, so there is nothing to leak - and the empty state says what is empty rather
        // than rendering the blank rectangle the baseline shipped.
        var html = await RenderMyDeliveriesAsync();

        Assert.DoesNotContain("Шевченко", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Петренко", html, StringComparison.Ordinal);
        Assert.Contains("Вам ще не призначено жодної доставки.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_own_deliveries_screen_still_renders_its_rows_and_marks_an_overdue_one()
    {
        var html = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);

        Assert.Contains("dt-table-scroll", html, StringComparison.Ordinal);
        Assert.Contains("Одна палета", html, StringComparison.Ordinal);
        Assert.Contains("Прострочено", html, StringComparison.Ordinal);
        Assert.Contains("У дорозі", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_own_deliveries_screen_is_the_six_columns_the_design_settles_on()
    {
        // The screen carried nine, and a nine-column table is unreadable on the phone half of its
        // audience. Three of them went: the weight, which is the same parcel the details describe;
        // the notes, which are a conversation about one delivery and belong to the timeline panel
        // this screen already opens; and the overdue flag, which is derived from the window and is
        // now a mark inside it.
        //
        // Asserted as the ordered list rather than as a count, because a count is satisfied by any
        // six columns at all - and the order is the hierarchy the design is about: when it is due
        // comes before where it is going.
        var html = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);

        var headings = HeaderCell.Matches(html)
            .Select(cell => Words(cell.Groups["body"].Value))
            .ToArray();

        Assert.Equal(
            ["Часове вікно", "Місце забирання", "Місце доставляння", "Вантаж", "Стан", "Дії"],
            headings);

        // And the overdue mark is inside the window rather than loose in a row that no longer has a
        // column for it. The equal count is what rules out a second home for it elsewhere.
        var windows = WindowCell.Matches(html);

        Assert.NotEmpty(windows);

        var marked = windows.Count(cell =>
            cell.Groups["body"].Value.Contains("dt-badge--overdue", StringComparison.Ordinal));

        Assert.NotEqual(0, marked);
        Assert.Equal(marked, SharedMarkup.Occurrences(html, "dt-badge--overdue"));
    }

    [Fact]
    public async Task The_merged_package_cell_carries_all_three_facts_about_the_parcel()
    {
        // Three columns became one, so the cell is the only place any of the three is now said -
        // and the delivery notes are the one that had nowhere else to go at all. A client types
        // them into the request form and the command stores them; DeliveryTimeline renders no notes
        // field, so dropping this line would leave the client's instructions write-only and the
        // driver standing at the door unable to read what the client asked for.
        var html = await RenderMyDeliveriesAsync(
            [OwnRow(details: "Одна палета", notes: "Зателефонувати перед прибуттям")]);

        var package = PackageCell.Match(html);

        Assert.True(package.Success, "The screen renders no package cell.");

        var cell = package.Groups["body"].Value;

        Assert.Contains("Одна палета", cell, StringComparison.Ordinal);
        Assert.Contains("Зателефонувати перед прибуттям", cell, StringComparison.Ordinal);

        // The weight with its unit on the line. The merged heading names the parcel rather than the
        // kilograms, so a bare figure here would be a number under a word that does not explain it.
        Assert.Contains(12.5m.ToString(Ukrainian) + " кг", Words(cell), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_request_action_stands_beside_the_heading_in_the_create_colour()
    {
        // The request action is about the collection the heading names rather than about any row in
        // it, so it belongs on that line - the shape the dispatch board's create already has. Loose
        // in a paragraph under the title it read as a caption, and it wore the edit colour while
        // the board's create wore the create one, which said the two were different kinds of act.
        var html = await RenderMyDeliveriesAsync(role: UserRole.Client);

        var head = PageHead.Match(html);

        Assert.True(head.Success, "The screen has no heading row.");
        Assert.Contains("<h1", head.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("dt-delivery-request", head.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("btn-primary", head.Groups["body"].Value, StringComparison.Ordinal);

        // A driver is offered nothing here, so their head is the heading alone rather than a
        // heading row with a hole where an action was styled away.
        var driver = PageHead.Match(await RenderMyDeliveriesAsync());

        Assert.True(driver.Success, "The screen has no heading row for a driver.");
        Assert.DoesNotContain("dt-delivery-request", driver.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_screen_asks_for_its_opening_view_and_draws_the_rows_in_the_order_they_came()
    {
        // The ordering is the server's now, so the screen's two duties are to ask for the view the
        // design opens on - page one of ten, earliest due first - and to draw the answer exactly as
        // it arrived. A screen that re-sorted the page would put these three rows back in window
        // order; this one must not touch them.
        var service = new StubDeliveryService(
        [
            OwnRow(id: 1, details: "Третя", windowLatest: Local(2026, 9, 19)),
            OwnRow(id: 2, details: "Перша", windowLatest: Local(2026, 9, 16)),
            OwnRow(id: 3, details: "Друга", windowLatest: Local(2026, 9, 17)),
        ]);

        var html = await RenderMyDeliveriesAsync(service: service);

        var asked = Assert.Single(service.AskedMine);

        Assert.Equal(0, asked.Offset);
        Assert.Equal(TableQuery.DefaultPageSize, asked.Limit);
        Assert.Equal(nameof(DeliverySort.Window), asked.Sort);
        Assert.False(asked.Descending);

        var rows = PackageCell.Matches(html)
            .Select(cell => cell.Groups["body"].Value)
            .ToArray();

        Assert.Equal(3, rows.Length);
        Assert.Contains("Третя", rows[0], StringComparison.Ordinal);
        Assert.Contains("Перша", rows[1], StringComparison.Ordinal);
        Assert.Contains("Друга", rows[2], StringComparison.Ordinal);

        // And the pager under them says how many there are.
        Assert.Contains("1–3 з 3 доставок", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(WindowShapes))]
    public async Task The_own_deliveries_window_cell_says_which_bounds_it_has(
        DateTimeOffset? earliest,
        DateTimeOffset? latest,
        string expected)
    {
        // The same four shapes the dispatch board's cell is asserted on, on this screen's render.
        // The reading is shared now - one `CellText.Window` for both - but the words that make a
        // single bound directional are in each screen's own markup, so a screen that lost them, or
        // crossed them, would render a bound with nothing saying which end it is while the board
        // stayed right.
        var html = await RenderMyDeliveriesAsync([OwnRow(windowEarliest: earliest, windowLatest: latest)]);

        var cell = WindowCell.Match(html);

        Assert.True(cell.Success, "The screen renders no window cell.");

        // The cell leads with its own name: the label that stands in for the column heading once the
        // head is dropped below the breakpoint, and which is therefore in the markup at every width.
        Assert.Equal("Часове вікно " + expected, Words(cell.Groups["body"].Value));
    }

    [Fact]
    public async Task Two_of_the_own_deliveries_headings_are_sort_controls_and_one_claims_a_direction()
    {
        // Rendered rather than reasoned about. Deleting the buttons, the `aria-sort` attributes and
        // the glyphs leaves the ordering cases green - they reach `Sort` directly - and leaves the
        // six-heading case green too, because it strips tags to read the words.
        var html = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);

        var sortable = HeaderCell.Matches(html)
            .Count(cell => cell.Groups["body"].Value.Contains("<button", StringComparison.Ordinal));

        // Two, and the other four headings are labels: an address and a package description are text
        // nobody compares across these rows, and the actions column names no value at all.
        Assert.Equal(2, sortable);
        Assert.Contains("dt-my-delivery-sort-window", html, StringComparison.Ordinal);
        Assert.Contains("dt-my-delivery-sort-status", html, StringComparison.Ordinal);

        // Exactly one claims a direction and the other claims none, so a screen reader is told which
        // column reordered the rows rather than hearing them silently rearrange.
        Assert.Equal(1, SharedMarkup.Occurrences(html, @"aria-sort=""ascending"""));
        Assert.Equal(1, SharedMarkup.Occurrences(html, @"aria-sort=""none"""));

        // And it is the window that claims it: a driver's and a client's question is what is due
        // next, and counting one ascending heading says a column is sorted without saying which.
        var claiming = SortedHeader.Match(html);

        Assert.True(claiming.Success, "No heading claims a sort direction.");
        Assert.Contains("Часове вікно", claiming.Groups["body"].Value, StringComparison.Ordinal);

        // The sighted half of the same fact, asserted on the drawn path rather than the enum member:
        // the sorted heading wears a chevron pointing the way the rows went, the other the stacked
        // pair that means "this sorts".
        Assert.Equal(1, SharedMarkup.Occurrences(html, AscendingGlyph));
        Assert.Equal(1, SharedMarkup.Occurrences(html, SortGlyph));
        Assert.Equal(0, SharedMarkup.Occurrences(html, DescendingGlyph));
    }
}
