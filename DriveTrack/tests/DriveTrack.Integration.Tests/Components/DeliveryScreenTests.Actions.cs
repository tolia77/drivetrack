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

/// <summary>What each delivery screen offers each role: actions, dialogs and the map.</summary>
public partial class DeliveryScreenTests
{
    [Fact]
    public async Task The_dispatch_board_renders_its_rows_through_the_shared_table()
    {
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        // The shared table, not a second one built beside it.
        Assert.Contains("dt-table-scroll", html, StringComparison.Ordinal);
        Assert.Contains("table-striped", html, StringComparison.Ordinal);

        // <h1> rather than <h2>: `FocusOnNavigate Selector="h1"` in Routes.razor looks for one, and
        // on a page without it a keyboard user keeps the focus the previous screen had.
        Assert.Contains("<h1", html, StringComparison.Ordinal);

        // Both parties named, which costs a second read the mapping has to make (Delivery has no
        // navigation to either), and the unassigned row saying so rather than rendering an empty
        // cell.
        Assert.Contains("Шевченко", html, StringComparison.Ordinal);
        Assert.Contains("Петренко", html, StringComparison.Ordinal);
        Assert.Contains("Без водія", html, StringComparison.Ordinal);
        Assert.Contains("Без замовника", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_location_with_no_resolved_address_shows_its_coordinates()
    {
        // An address is a cache the geocoder may not have filled yet: a location stored before it
        // resolves has a null address, so a screen that rendered the address alone would show a
        // column of empty cells and nothing would say why.
        var html = await RenderDeliveriesAsync(UserRole.Dispatcher);

        Assert.Contains("50.4501; 30.5234", html, StringComparison.Ordinal);

        // And the other arm: once the geocoder fills the cache, the address is what is shown.
        Assert.Contains("Львів, площа Ринок", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_driver_is_offered_no_way_to_open_a_delivery_at_all()
    {
        // The distinction the two screens exist to make, as a driver sees it. Opening, editing and
        // deleting a delivery on somebody's behalf is dispatch's, and asking for one is the client's
        // - so a driver gets none of it, and an action offered here could only ever be
        // refused.
        //
        // The timeline is the exception, and it is not a counter-example: a driver advances the
        // parcel they are carrying and a client adds a note to their own delivery, so that dialog
        // belongs on both screens. What must not appear is a form that opens a delivery -
        // dispatch's or the client's.
        var dispatch = await RenderDeliveriesAsync(UserRole.Dispatcher);
        var mine = await RenderMyDeliveriesAsync();

        Assert.Contains("btn btn-primary", dispatch, StringComparison.Ordinal);
        Assert.Contains("Створити", dispatch, StringComparison.Ordinal);

        Assert.DoesNotContain("btn btn-primary", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("Створити", mine, StringComparison.Ordinal);

        // No dispatch form, no edit form and no deletion prompt: the three dialogs the dispatch
        // board carries and this screen has no operation for.
        Assert.DoesNotContain("delivery-form", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-delivery-edit", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-confirm-accept", mine, StringComparison.Ordinal);

        // And none of the client's request markup either. Withheld rather than hidden: the dialog is
        // not rendered at all for a driver, so there is nothing on the page to reach.
        Assert.DoesNotContain("dt-delivery-request", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("request-form", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("Замовити", mine, StringComparison.Ordinal);

        // And the count, because the named checks above only rule out the dialogs that exist today:
        // a further one, added for some later capability, would slip past every one of them.
        //
        // Three for a driver, and each is an operation they genuinely have: the proof capture a
        // driver performs at the door and the proof either role may read afterwards, and the
        // read-only map, which the dispatch board also has. The timeline is a fourth, but it owns
        // its dialog and is rendered only once a row is chosen, so it is not on the page until
        // then. The number is pinned rather than relaxed for the reason it was pinned at one: it is
        // the only assertion that notices a dialog belonging to another role arriving here by a
        // paste.
        Assert.Equal(3, SharedMarkup.Occurrences(mine, "<dialog"));
    }

    [Fact]
    public async Task A_client_is_offered_the_request_action_and_the_form_behind_it()
    {
        // Client delivery requests at the surface. The same screen, the same dialogs a
        // driver gets, plus one: the request form, which exists for exactly one role.
        var mine = await RenderMyDeliveriesAsync(role: UserRole.Client);

        Assert.Contains("dt-delivery-request", mine, StringComparison.Ordinal);
        Assert.Contains("Замовити доставку", mine, StringComparison.Ordinal);

        // The form itself, and the two address search boxes above the maps.
        Assert.Contains("request-form", mine, StringComparison.Ordinal);
        Assert.Contains("dt-request-pickup-search", mine, StringComparison.Ordinal);
        Assert.Contains("dt-request-dropoff-search", mine, StringComparison.Ordinal);
        Assert.Contains("dt-request-submit", mine, StringComparison.Ordinal);

        // And what the form must not carry, which is the product rule rather than a styling choice:
        // a request names no driver, no client and no window, because RequestDeliveryCommand has no
        // field to bind one to.
        Assert.DoesNotContain("delivery-driver", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("delivery-client", mine, StringComparison.Ordinal);

        // The ids the dispatch board's window inputs actually carry, so a block pasted across from
        // it is what this catches - an id only this assertion has ever named would catch nothing.
        Assert.DoesNotContain("delivery-window-earliest", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("delivery-window-latest", mine, StringComparison.Ordinal);

        // Nor dispatch's own create action, which the driver's test rules out for a driver and
        // nothing ruled out here: a client asks for a delivery of their own and opens none for
        // anybody else, so the create button belongs on the dispatch board alone.
        //
        // Named by its hook rather than by its variant. The variant stopped identifying it once
        // the request submit became a create too - both wear `.btn-primary`, correctly, because
        // both create something. A colour is what an action means, never which action it is.
        Assert.DoesNotContain("dt-delivery-create", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("Створити", mine, StringComparison.Ordinal);

        // Still not the dispatch board's form, and still not its edit or delete prompts.
        Assert.DoesNotContain("delivery-form", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-delivery-edit", mine, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-confirm-accept", mine, StringComparison.Ordinal);

        // The driver's three plus the request form; the timeline's dialog is not on the page until
        // a row is chosen.
        Assert.Equal(4, SharedMarkup.Occurrences(mine, "<dialog"));
    }

    [Fact]
    public async Task A_location_cell_is_an_action_on_every_screen_that_shows_one()
    {
        // The read-only map names no role: clicking a location cell opens a map centred on that
        // coordinate, for a driver and a client as much as for dispatch. Without it the
        // own-deliveries screen renders the same addresses as dead text.
        //
        // What is asserted is that the cell is a button rather than a label, because static
        // rendering cannot click it - the same limit the dispatch board's own map test records.
        var dispatch = await RenderDeliveriesAsync(UserRole.Dispatcher);
        var driver = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);
        var customer = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned, UserRole.Client);

        Assert.Contains("dt-delivery-pickup", dispatch, StringComparison.Ordinal);
        Assert.Contains("dt-delivery-dropoff", dispatch, StringComparison.Ordinal);

        Assert.Contains("dt-my-delivery-pickup", driver, StringComparison.Ordinal);
        Assert.Contains("dt-my-delivery-dropoff", driver, StringComparison.Ordinal);

        // A client reads their own delivery's addresses too, and what is withheld from them is the
        // driver, not the destination.
        Assert.Contains("dt-my-delivery-pickup", customer, StringComparison.Ordinal);
        Assert.Contains("dt-my-delivery-dropoff", customer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UserRole.Client, true)]
    [InlineData(UserRole.Driver, false)]
    [InlineData(UserRole.Dispatcher, false)]
    [InlineData(UserRole.Admin, false)]
    public void Only_a_client_is_offered_the_request_action(UserRole role, bool offered)
    {
        // Asserted directly for the reason the other two predicates on this screen are: the flag
        // decides whether the dialog is rendered at all, and inverting it would put a request form
        // on a driver's page - markup IAccessGuard.RequireDeliveryComposer would refuse the moment
        // it was used, but markup that should never have been drawn.
        Assert.Equal(offered, Web.Components.Pages.Deliveries.MyDeliveriesPage.OffersRequest(role));
    }

    [Fact]
    public async Task Both_screens_offer_the_timeline_and_neither_pays_for_it_until_it_is_opened()
    {
        // The timeline reaches every role that can see a delivery, so the action is on both
        // screens. The panel inside the dialog is built only once a row has been chosen - a
        // component per row would issue a timeline read per row on first paint, which is the cost
        // this shape avoids and which nothing else in the suite would notice.
        var dispatch = await RenderDeliveriesAsync(UserRole.Dispatcher);
        var mine = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);

        Assert.Contains("dt-delivery-timeline", dispatch, StringComparison.Ordinal);
        Assert.Contains("dt-delivery-timeline", mine, StringComparison.Ordinal);

        // The stub's entry would render its actor's name if the panel had been built.
        Assert.DoesNotContain("dt-timeline-list", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-timeline-list", mine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Both_screens_offer_the_proof_and_only_the_driver_is_offered_the_capture()
    {
        // Proof reaches every role that can see a delivery, so the read action is on both screens;
        // capture happens at a door, so it is the driver's alone and appears on neither the
        // dispatch board nor a client's list. Without this, deleting either button leaves the whole
        // suite green - the dialog count next door is about the own-deliveries screen and counts
        // dialogs, not the actions that open them.
        var dispatch = await RenderDeliveriesAsync(UserRole.Dispatcher);
        var driver = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned);
        var customer = await RenderMyDeliveriesAsync(StubDeliveryService.Assigned, UserRole.Client);

        Assert.Contains("dt-proof-open", dispatch, StringComparison.Ordinal);
        Assert.Contains("dt-proof-open", driver, StringComparison.Ordinal);
        Assert.Contains("dt-proof-open", customer, StringComparison.Ordinal);

        Assert.Contains("dt-proof-capture", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-proof-capture", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-proof-capture", customer, StringComparison.Ordinal);

        // And neither panel is built until a row is chosen, as the timeline's is not: a component
        // per row would issue a proof read per row on first paint.
        Assert.DoesNotContain("dt-proof-facts", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-proof-facts", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-proof-form", driver, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_reaches_the_own_deliveries_screen_and_its_history_action()
    {
        // The screen serves both narrowed roles, and nothing else in the suite renders it as a
        // client: they get the note field, so the row action has to be there for them too.
        var html = await RenderMyDeliveriesAsync(
            StubDeliveryService.Assigned, UserRole.Client);

        Assert.Contains("dt-delivery-timeline", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UserRole.Driver, true)]
    [InlineData(UserRole.Client, false)]
    [InlineData(UserRole.Dispatcher, false)]
    [InlineData(UserRole.Admin, false)]
    public void Only_a_driver_is_drawn_the_transition_buttons_on_their_own_deliveries(
        UserRole role,
        bool expected)
    {
        // A client never changes status, asserted where it actually lives. The render above cannot
        // make this claim: the timeline panel is built only after a row is chosen, and static
        // rendering dispatches no event that could choose one - so inverting the flag would leave
        // every render assertion in this file green while a client was handed buttons that must not
        // be drawn.
        //
        // The two dispatch roles are false here and that is not a contradiction: they read their
        // deliveries on /deliveries, where the panel is opened with the flag set outright.
        Assert.Equal(expected, Web.Components.Pages.Deliveries.MyDeliveriesPage.OffersStatusChange(role));
    }

    [Fact]
    public async Task Only_an_administrator_is_offered_the_deletion_prompt()
    {
        // Deletion is an admin's alone, and IAccessGuard refuses a dispatcher who asks. The markup
        // hides, the guard decides.
        var admin = await RenderDeliveriesAsync(UserRole.Admin);
        var dispatcher = await RenderDeliveriesAsync(UserRole.Dispatcher);

        Assert.Contains("dt-delivery-delete", admin, StringComparison.Ordinal);
        Assert.Contains("dt-confirm-accept", admin, StringComparison.Ordinal);

        // The prompt is the native element with the ARIA trio written out.
        Assert.Contains(@"aria-modal=""true""", admin, StringComparison.Ordinal);

        Assert.DoesNotContain("dt-delivery-delete", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-confirm-accept", dispatcher, StringComparison.Ordinal);

        // The edit action is a dispatcher's as much as an admin's, so hiding the delete must not
        // have hidden the row's other action with it.
        Assert.Contains("dt-delivery-edit", dispatcher, StringComparison.Ordinal);
    }
}
