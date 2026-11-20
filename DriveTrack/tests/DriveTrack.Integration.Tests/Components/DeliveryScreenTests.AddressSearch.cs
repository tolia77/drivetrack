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

/// <summary>The two address search boxes on the delivery and request forms.</summary>
public partial class DeliveryScreenTests
{
    // =====================================================================================
    // The two address search boxes
    //
    // Static rendering dispatches no events, so nothing above can press a search button or click
    // one of its answers. The state and the transitions therefore live on the form, where they can
    // be driven directly - which is the only way the wiring between a box and the point it fills
    // is asserted at all. Cross the two and every render assertion in this file still passes.
    // =====================================================================================

    [Fact]
    public async Task Each_box_searches_its_own_query()
    {
        // The mistake this exists for: SearchDropoffAsync sending the pickup box's text. Both boxes
        // look identical in the markup and a rendered page cannot tell the two apart.
        var form = new DeliveryForm();
        var asked = new List<string?>();

        form.PickupSearch.Query = "Хрещатик";
        form.DropoffSearch.Query = "Площа Ринок";

        await form.DropoffSearch.RunAsync(Recording(asked, []), TestContext.Current.CancellationToken);

        Assert.Equal(new string?[] { "Площа Ринок" }, asked);

        await form.PickupSearch.RunAsync(Recording(asked, []), TestContext.Current.CancellationToken);

        Assert.Equal(new string?[] { "Площа Ринок", "Хрещатик" }, asked);
    }

    [Fact]
    public async Task Choosing_a_match_sets_that_box_s_point_and_leaves_the_other_alone()
    {
        // The other half of the same mistake: ChooseDropoff assigning _form.Pickup. The dropoff map
        // would then stay where it was and the pickup map would jump to an address nobody chose for
        // it, and no render test could see either.
        var form = new DeliveryForm();
        var match = new PlaceMatch("Львів, площа Ринок, 1", new MapLocation(49.8419, 24.0315));

        await form.DropoffSearch.RunAsync(Answering([match]), TestContext.Current.CancellationToken);

        Assert.Single(form.DropoffSearch.Matches);
        Assert.Empty(form.PickupSearch.Matches);

        form.DropoffSearch.Choose(match);

        Assert.Equal(match.Point, form.Dropoff);
        Assert.Null(form.Pickup);

        // The list is cleared with the choice: one left standing beside a map that has already moved
        // invites a second click on a match that has already been applied.
        Assert.Empty(form.DropoffSearch.Matches);
        Assert.False(form.DropoffSearch.FoundNothing);
    }

    [Fact]
    public async Task A_search_that_matched_nothing_says_so_and_an_untouched_box_says_nothing()
    {
        // "No search has run" and "a search found nothing" are different states, and only the second
        // has anything to tell the dispatcher. One flag for both would make the hint appear under a
        // box nobody has used yet.
        var form = new DeliveryForm();

        Assert.False(form.PickupSearch.FoundNothing);

        await form.PickupSearch.RunAsync(Answering([]), TestContext.Current.CancellationToken);

        Assert.True(form.PickupSearch.FoundNothing);
        Assert.False(form.DropoffSearch.FoundNothing);
    }

    [Fact]
    public async Task A_refused_search_answers_the_failure_keys_and_leaves_the_box_usable()
    {
        // The 422 a query under three characters earns. The screen renders these keys through the
        // same catalogue the REST envelope uses, and the box has to come back out of its
        // busy state or the button stays disabled and reads as a hung screen.
        //
        // The refusal is left on the box rather than returned: a caller handed it can put it
        // anywhere, and where it used to be put was the screen's one failure field, which the submit
        // path also writes.
        var form = new DeliveryForm();

        await form.PickupSearch.RunAsync(
            (_, _) => throw new ValidationException(
                ErrorCode.COMMON_VALIDATION_FAILED,
                "Too short.",
                []),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(form.PickupSearch.Failures);
        Assert.False(form.PickupSearch.IsBusy);
        Assert.Empty(form.PickupSearch.Matches);

        // And it is not also reported as a search that found nothing. The box's banner renders
        // directly above that line, so both at once would answer one click with two different
        // sentences - "this is why it was refused" and "there was nothing to find".
        Assert.False(form.PickupSearch.FoundNothing);
    }

    [Fact]
    public async Task A_box_is_busy_only_while_its_own_lookup_is_running()
    {
        // The busy flag is what disables the button, so an impatient second click cannot become a
        // second request to a public geocoder. Per box: searching for a pickup must not disable the
        // dropoff box.
        var form = new DeliveryForm();
        var observed = new List<(bool Pickup, bool Dropoff)>();

        await form.PickupSearch.RunAsync(
            (_, _) =>
            {
                observed.Add((form.PickupSearch.IsBusy, form.DropoffSearch.IsBusy));

                return Task.FromResult<IReadOnlyList<PlaceMatch>>([]);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal([(true, false)], observed);
        Assert.False(form.PickupSearch.IsBusy);
    }

    [Fact]
    public async Task Each_box_on_the_request_form_searches_its_own_query()
    {
        // The client's form builds its own pair of boxes, so it can cross them in its own
        // constructor - and swapping the two lambdas there compiles, renders byte-identical markup
        // and leaves every render assertion in this file green. This is the only thing that sees it.
        var form = new RequestForm();
        var asked = new List<string?>();

        form.PickupSearch.Query = "Хрещатик";
        form.DropoffSearch.Query = "Площа Ринок";

        await form.DropoffSearch.RunAsync(Recording(asked, []), TestContext.Current.CancellationToken);

        Assert.Equal(new string?[] { "Площа Ринок" }, asked);

        await form.PickupSearch.RunAsync(Recording(asked, []), TestContext.Current.CancellationToken);

        Assert.Equal(new string?[] { "Площа Ринок", "Хрещатик" }, asked);
    }

    [Fact]
    public async Task Choosing_a_match_on_the_request_form_sets_that_box_s_point_and_leaves_the_other_alone()
    {
        // The other half of the same mistake, on the client's form: a dropoff box wired to the
        // pickup point would leave the dropoff map where it was and jump the pickup map to an
        // address nobody chose for it, and the request would be sent with both.
        var form = new RequestForm();
        var match = new PlaceMatch("Львів, площа Ринок, 1", new MapLocation(49.8419, 24.0315));

        await form.DropoffSearch.RunAsync(Answering([match]), TestContext.Current.CancellationToken);

        Assert.Single(form.DropoffSearch.Matches);
        Assert.Empty(form.PickupSearch.Matches);

        form.DropoffSearch.Choose(match);

        Assert.Equal(match.Point, form.Dropoff);
        Assert.Null(form.Pickup);

        // And the pickup box fills the pickup point, which is the half a one-sided assertion would
        // pass for a form that wired both boxes to the same field.
        var pickup = new PlaceMatch("Київ, вулиця Хрещатик, 1", new MapLocation(50.4472, 30.5222));

        form.PickupSearch.Choose(pickup);

        Assert.Equal(pickup.Point, form.Pickup);
        Assert.Equal(match.Point, form.Dropoff);
    }

    [Fact]
    public void The_request_form_sends_each_point_as_the_field_it_filled()
    {
        // The last place the pair can be crossed: the two points are adjacent arguments of the same
        // type in the command's constructor, so swapping them compiles, renders byte-identical
        // markup and leaves every assertion above green - and the parcel is then collected at the
        // address it was meant to be delivered to.
        var form = new RequestForm
        {
            Pickup = new MapLocation(50.4472, 30.5222),
            Dropoff = new MapLocation(49.8419, 24.0315),
            PackageDetails = "Одна палета",
            PackageWeightKg = 12.5m,
            DeliveryNotes = "Подзвонити за годину",
        };

        var command = form.ToCommand();

        Assert.Equal(new LocationInput(50.4472, 30.5222), command.Pickup);
        Assert.Equal(new LocationInput(49.8419, 24.0315), command.Dropoff);
        Assert.Equal("Одна палета", command.PackageDetails);
        Assert.Equal(12.5m, command.PackageWeightKg);
        Assert.Equal("Подзвонити за годину", command.DeliveryNotes);
    }

    [Fact]
    public void An_unpicked_point_is_sent_as_nothing_rather_than_as_a_guess()
    {
        // Null rather than a zero coordinate, which is a real place off the coast of Africa: the
        // capability refuses an absent point by name, and a guessed one it would accept.
        var command = new RequestForm().ToCommand();

        Assert.Null(command.Pickup);
        Assert.Null(command.Dropoff);
    }

    /// <summary>A search that records the query it was given and answers a fixed list.</summary>
    private static Func<SearchPlacesQuery, CancellationToken, Task<IReadOnlyList<PlaceMatch>>> Recording(
        List<string?> asked,
        IReadOnlyList<PlaceMatch> matches) =>
        (query, _) =>
        {
            asked.Add(query.Query);

            return Task.FromResult(matches);
        };

    /// <inheritdoc cref="Recording" />
    private static Func<SearchPlacesQuery, CancellationToken, Task<IReadOnlyList<PlaceMatch>>> Answering(
        IReadOnlyList<PlaceMatch> matches) =>
        (_, _) => Task.FromResult(matches);
}
