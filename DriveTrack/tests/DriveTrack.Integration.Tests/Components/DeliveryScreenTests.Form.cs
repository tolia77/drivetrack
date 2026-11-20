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

/// <summary>The delivery form: its two location pickers, the driver picker and the capacity hint.</summary>
public partial class DeliveryScreenTests
{
    [Fact]
    public async Task The_form_embeds_two_pickers_and_a_location_cell_opens_a_third_map()
    {
        // One map component, two jobs. Two pickers are rendered with the
        // form; the read-only one is built when a cell is clicked, which static rendering cannot
        // do - so what is asserted here is that the cell is an action rather than a dead label.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        Assert.Equal(2, SharedMarkup.Occurrences(html, @"class=""dt-map"""));
        Assert.Contains("dt-delivery-pickup", html, StringComparison.Ordinal);
        Assert.Contains("dt-delivery-dropoff", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Both_delivery_dialogs_lay_their_two_pickers_out_the_same_way()
    {
        // The two dialogs are the same dialog for two roles, and their field sets differ only where
        // a client's request has to. Their *presentation* is not supposed to differ at all -
        // and it silently did: the wide panel and the two-column picker grid were given to the
        // dispatch board and not to the request form, so a client aimed at a full-width map with the
        // second one below the fold while a dispatcher read both side by side.
        //
        // Both screens go through the same assertions in one case rather than one case each, because
        // what regressed was never one screen: it was a change applied to one twin and not the
        // other, and a case that reads both is what notices that.
        AssertPickersShareOneRow(await RenderDeliveriesAsync(UserRole.Dispatcher), "delivery-form");
        AssertPickersShareOneRow(await RenderMyDeliveriesAsync(role: UserRole.Client), "request-form");
    }

    /// <summary>
    /// One screen's two place pickers, asserted to be the two columns of one row inside the wide
    /// dialog that holds the form.
    /// </summary>
    /// <remarks>
    /// Every assertion is scoped to that one panel rather than to the page, because page-wide
    /// occurrence counts cannot see the structure and this regression is structural. A screen
    /// renders four or five dialogs, so a page-wide <c>Contains</c> on the wide class would stay
    /// green with the wide panel on the proof viewer and the form back at 32rem; and two columns in
    /// two separate rows - the stacked layout this case exists to forbid - still count two. Scoped
    /// to the panel, what is pinned is the containment the layout actually is: the form's own dialog
    /// is the wide one, it holds exactly one row, and both columns are inside that row.
    /// </remarks>
    private static void AssertPickersShareOneRow(string html, string formId)
    {
        var form = html.IndexOf($@"id=""{formId}""", StringComparison.Ordinal);

        Assert.True(form > 0, $"No element carries id=\"{formId}\".");

        // The dialog the form sits in: the last one opened above it. DtDialog renders a flat
        // <dialog> per instance and never nests them, so the nearest opening tag is the enclosing
        // one - and slicing there is what keeps the other four dialogs out of every assertion below.
        var panel = html[html.LastIndexOf("<dialog", form, StringComparison.Ordinal)..form];

        Assert.Contains("dt-dialog-wide", panel, StringComparison.Ordinal);

        var row = panel.IndexOf(@"class=""row g-4 mb-4""", StringComparison.Ordinal);

        Assert.True(row >= 0, "The two pickers are not laid out in a row.");
        Assert.Equal(1, SharedMarkup.Occurrences(panel, @"class=""row g-4 mb-4"""));

        // `col-lg-6` as a class token rather than as a whole attribute value: a column that gained a
        // second class would still be a column, and failing on that would report drift where there
        // is none.
        var columns = Column.Matches(panel).Select(match => match.Index).ToArray();

        Assert.Equal(2, columns.Length);
        Assert.All(columns, column => Assert.True(column > row, "A picker column sits outside the row."));
    }

    [Fact]
    public async Task The_form_shows_the_capacity_the_dispatcher_is_about_to_exceed()
    {
        // The capacity rejection names both figures, and the envelope has one message
        // per code - so the capacity reaches the dispatcher here, beside the weight input, at the
        // moment they could exceed it.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        Assert.Contains("Вантажопідйомність автомобіля обраного водія", html, StringComparison.Ordinal);
        Assert.Contains(@"id=""delivery-weight""", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_driver_picker_names_each_drivers_standing_and_whether_they_are_on_duty()
    {
        // Rating and duty state at the moment a dispatcher chooses who carries a parcel. An
        // <option> holds text and nothing else, so both the rating and the duty marker are composed
        // in C# - which means all four branches are only covered if the stub has one driver of each
        // kind.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        var options = Regex.Matches(
                html,
                @"<option\b[^>]*>(?<body>.*?)</option>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5))
            .Select(match => System.Net.WebUtility.HtmlDecode(match.Groups["body"].Value).Trim())
            .ToArray();

        // 4,5 under uk-UA, which uses a comma for the decimal mark: the average is rendered
        // as an average rather than rounded to a whole star. The marker after it is the driver's
        // open shift, in the same words the shift screen's own driver picker uses - and not the
        // words of the state badge beside it, which labels a shift rather than a person.
        Assert.Contains("Тарас Шевченко — 4,5 — На зміні", options);

        // And the driver nobody has reviewed, who gets the catalogue's "no value" text. Never a
        // zero: that is a rating, and the worst one the scale has.
        //
        // The duty marker's other half, and the one worth stating flatly: this driver is off duty
        // and is still in the list. The requirement asks the form to flag rather than to refuse, so
        // an
        // <option> that had been filtered out - or disabled - would fail here rather than quietly
        // turning a note into a rule the guard never agreed to.
        Assert.Contains("Олег Коваль — Немає оцінок — Не на зміні", options);

        var offDuty = Regex.Match(
            html,
            @"<option\b[^>]*>[^<]*Олег Коваль[^<]*</option>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(offDuty.Success, "The picker dropped the off-duty driver instead of marking them.");
        Assert.DoesNotContain("disabled", offDuty.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_capacity_hint_reads_the_vehicle_the_selected_driver_holds()
    {
        // The other half of the assertion above, and the half the render cannot make: static
        // rendering dispatches no events, so the form has no driver selected and the hint is a dash
        // in that test whatever the lookup does. Keying the lookup on the wrong id, or dropping the
        // fleet fetch that feeds it, would leave the hint a dash for every driver and change
        // nothing above - so the number is checked here instead.
        var drivers = new[]
        {
            new DriverSummary(
                new DriverId(1),
                new UserId(10),
                "Тарас",
                "Шевченко",
                "taras@drivetrack.test",
                "ВІ123456",
                5,
                "Рено Мастер",
                "АА1234ВВ",
                Rating: null,
                ReviewCount: 0,

                // Duty state has no part in the capacity lookup below, and saying so is the point
                // of stating it: a driver's duty state must not change which vehicle they hold.
                OnDuty: true),
            new DriverSummary(
                new DriverId(2),
                new UserId(11),
                "Іван",
                "Коваль",
                "ivan@drivetrack.test",
                "ВІ654321",
                VehicleId: null,
                VehicleModel: null,
                VehicleLicensePlate: null,
                Rating: null,
                ReviewCount: 0,
                OnDuty: false),
        };

        var vehicles = new[]
        {
            new VehicleSummary(5, "Рено Мастер", "АА1234ВВ", 1200m, 42_000, new DateOnly(2026, 12, 1)),
        };

        // The driver holding vehicle 5 shows that vehicle's capacity, not another's and not a dash.
        Assert.Equal(1200m, Web.Components.Pages.Deliveries.DeliveriesPage.CapacityOf(drivers, vehicles, driverId: 1));

        // A driver without a vehicle is legal, and the capacity rule leaves them unchecked: there
        // is no figure to show, which is the one case the dash legitimately means.
        Assert.Null(Web.Components.Pages.Deliveries.DeliveriesPage.CapacityOf(drivers, vehicles, driverId: 2));

        // No driver named yet, and a driver the page never fetched: neither invents a capacity.
        Assert.Null(Web.Components.Pages.Deliveries.DeliveriesPage.CapacityOf(drivers, vehicles, driverId: null));
        Assert.Null(Web.Components.Pages.Deliveries.DeliveriesPage.CapacityOf(drivers, vehicles, driverId: 99));

        // The driver holds a vehicle that fell outside the page of fleet rows the form fetched.
        Assert.Null(Web.Components.Pages.Deliveries.DeliveriesPage.CapacityOf(drivers, [], driverId: 1));
    }

    [Theory]
    [InlineData("DeliveriesPage.razor", "_form", "")]
    [InlineData("MyDeliveriesPage.razor", "_request", "request-")]
    public void Each_address_box_is_wired_to_the_point_it_is_labelled_with(
        string fileName,
        string model,
        string stem)
    {
        // The join the PlaceSearch cases below leave open, and the same shape of hole the sort
        // headings above have: driving the holders directly proves each one searches its own query
        // and assigns its own point, but nothing there can tell whether the picker *labelled*
        // dropoff was handed the dropoff holder. Swap one attribute and a dispatcher's pickup search
        // fills the dropoff list and moves the dropoff map, with every test in this file still green.
        //
        // Read from source, because a statically rendered page carries no trace of what a component
        // was handed - both pickers render the same column whichever holder they got. And read per
        // call rather than by counting the page, because what can be crossed now lives inside one
        // tag: five attributes that have to agree on one point.
        var calls = Picker.Matches(SharedMarkup.ReadComponent("Pages", fileName));

        Assert.Equal(2, calls.Count);

        foreach (var point in new[] { "Pickup", "Dropoff" })
        {
            var call = Assert.Single(
                calls,
                match => AttributeOf(match, "Prefix") == stem + point.ToLowerInvariant());

            Assert.Equal(point, AttributeOf(call, "Field"));
            Assert.Equal($"{model}.{point}Search", AttributeOf(call, "Search"));
            Assert.Equal($"{model}.{point}", AttributeOf(call, "Value"));

            // The two the screen still spells for itself: the handler that writes the point back,
            // and the label above the map. Both name the point, whatever the screen prefixes them
            // with - `PickPickup` here and `PickRequestPickup` on the client's dialog.
            Assert.Contains(point, AttributeOf(call, "ValueChanged"), StringComparison.Ordinal);
            Assert.Contains(
                $@"Localizer[""{point}""]",
                call.Groups["attributes"].Value,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("request-dropoff")]
    [InlineData("pickup")]
    public async Task The_picker_writes_its_caller_s_stem_into_every_hook(string prefix)
    {
        // The four positions the suite pins - the search box's id, its button, the found-nothing
        // line and every match - are what tells one picker from the other, on the page and in every
        // assertion about it. They are now written once, from one parameter, so a stem interpolated
        // into three of the four and left literal in the fourth is a hook belonging to the wrong box
        // on all four pickers at once. Asserted here rather than per screen, which is the whole
        // point of there being one component.
        //
        // Two stems rather than one, because a single stem is satisfied by a component with all four
        // hooks hard-coded to that very word - stray-stem loop included. The second render is what
        // makes it a parameter rather than a literal that happened to be right.
        //
        // Two renders per stem, because the two conditional hooks are mutually exclusive by
        // construction: a box that found nothing has no matches to list.
        var found = new PlaceMatch("Київ, вулиця Хрещатик, 1", new MapLocation(50.4472, 30.5222));

        var empty = await RenderPickerAsync(prefix, Answering([]));
        var matched = await RenderPickerAsync(prefix, Answering([found]));

        Assert.Contains($@"id=""{prefix}-search""", empty, StringComparison.Ordinal);
        Assert.Contains($@"for=""{prefix}-search""", empty, StringComparison.Ordinal);
        Assert.Contains($"dt-{prefix}-search", empty, StringComparison.Ordinal);
        Assert.Contains($"dt-{prefix}-empty", empty, StringComparison.Ordinal);
        Assert.DoesNotContain($"dt-{prefix}-match", empty, StringComparison.Ordinal);

        Assert.Contains($"dt-{prefix}-match", matched, StringComparison.Ordinal);
        Assert.Contains(found.Address, matched, StringComparison.Ordinal);
        Assert.DoesNotContain($"dt-{prefix}-empty", matched, StringComparison.Ordinal);

        // And no other picker's stem is left behind: a position written as a literal instead of from
        // the parameter is one of the other three showing through.
        foreach (var stray in Stems.Where(stem => stem != prefix))
        {
            foreach (var html in new[] { empty, matched })
            {
                Assert.DoesNotContain($"dt-{stray}-", html, StringComparison.Ordinal);
                Assert.DoesNotContain($@"id=""{stray}-search""", html, StringComparison.Ordinal);
            }
        }

        // The caption the caller gives the map, and the refusal the caller's sink carries about the
        // point beneath it: the two things this component renders on somebody else's behalf, and the
        // two whose omission leaves a page that still looks entirely right. The message is placed
        // rather than merely present - above the map it would read as a sentence about the search
        // box, which is the box that did not produce it.
        Assert.Contains(PickerLabel, empty, StringComparison.Ordinal);

        var map = empty.IndexOf(@"class=""dt-map""", StringComparison.Ordinal);
        var message = empty.IndexOf(@"class=""form-text dt-field-error""", StringComparison.Ordinal);

        Assert.True(map >= 0, "The picker renders no map.");
        Assert.True(message > map, "The refusal about the point is not rendered beneath the map.");
    }

    [Theory]
    [InlineData("private Task ChooseAsync(PlaceMatch match)")]
    [InlineData("private Task Picked(MapLocation point)")]
    public void The_picker_tells_its_caller_the_point_moved_however_it_was_picked(string handler)
    {
        // What a static render cannot reach, and what every assertion above survives: replace either
        // body with Task.CompletedTask and the build is clean and the whole suite green, while a
        // chosen address leaves the map on the point it was already showing and a map click writes
        // nothing at all. An event handled inside a component re-renders that component and nothing
        // above it, so this callback is the only thing that tells the screen its field has moved.
        //
        // Read from source for the reason the wiring case above is: a rendered page carries no trace
        // of what a handler does, and this handler cannot be pressed here.
        var picker = SharedMarkup.ReadShared("DtLocationPicker.razor");

        Assert.Contains(
            "ValueChanged.InvokeAsync(",
            MemberBody(picker, handler),
            StringComparison.Ordinal);
    }

    /// <summary>One picker, rendered over a box that has already answered.</summary>
    /// <remarks>
    /// The box is driven before the render rather than through it: static rendering dispatches no
    /// events, so the only way to read the found-nothing line and the match list back is to hand the
    /// component a box that has already run.
    /// </remarks>
    private static async Task<string> RenderPickerAsync(
        string prefix,
        Func<SearchPlacesQuery, CancellationToken, Task<IReadOnlyList<PlaceMatch>>> search)
    {
        var box = new PlaceSearch(_ => { });

        await box.RunAsync(search, TestContext.Current.CancellationToken);

        return await ComponentRenderer.RenderAsync<DtLocationPicker>(
            new Dictionary<string, object?>
            {
                ["Prefix"] = prefix,
                ["Label"] = PickerLabel,
                ["Field"] = "Dropoff",
                ["Search"] = box,

                // The screen's own sink, carrying the refusal an unpicked point earns. Bound so the
                // control beneath the map has something to claim: empty, it renders nothing, and
                // "the message is under the map" would be a sentence about an absence.
                ["Failures"] = (IReadOnlyList<ScreenFailure>)
                [
                    new ScreenFailure("Dropoff", nameof(ErrorCode.COMMON_FIELD_REQUIRED)),
                ],
            },
            services => services.AddSingleton<IDeliveryService>(new StubDeliveryService()));
    }

    /// <summary>
    /// One member of a component's <c>@code</c> block, from its signature to whatever is documented
    /// next. Every member in that file carries a doc comment, so the next <c>///</c> at member
    /// indentation is where this one ends.
    /// </summary>
    private static string MemberBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);

        Assert.True(start >= 0, $"No member declared as '{signature}'.");

        var body = source[(start + signature.Length)..];
        var next = body.IndexOf("\n    /// ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    /// <summary>What one attribute of a <c>&lt;DtLocationPicker&gt;</c> call was given.</summary>
    private static string AttributeOf(Match call, string name) =>
        Regex.Match(
            call.Groups["attributes"].Value,
            $@"\b{name}\s*=\s*""(?<value>[^""]*)""",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5))
            .Groups["value"].Value;
}
