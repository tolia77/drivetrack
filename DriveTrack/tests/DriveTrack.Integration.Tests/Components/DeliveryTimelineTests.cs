using System.Text.RegularExpressions;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using DriveTrack.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// What the timeline panel actually renders (FR-105 to FR-108).
/// <para>
/// A source scan cannot make these claims. "A viewer who may not be told the actor's name is shown
/// their role instead" and "the buttons are exactly the moves the lifecycle allows" are properties
/// of the output, and the file contains all the same words whether they hold or not.
/// </para>
/// <para>
/// Rendering is static, so nothing here presses a button. What is asserted is what a viewer is
/// offered — which is the half FR-12 is about, since pressing one is refused or allowed by
/// <c>IAccessGuard</c> and the API suite asserts that.
/// </para>
/// </summary>
public class DeliveryTimelineTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The label each status renders, as the catalogue holds it. Written out rather than resolved
    /// through a localizer, because what is under test is that the component reaches the right key:
    /// asserting against the same localizer the component reads would pass for every wiring.
    /// </summary>
    private static readonly Dictionary<DeliveryStatus, string> Labels = new()
    {
        [DeliveryStatus.Pending] = "Очікує",
        [DeliveryStatus.InTransit] = "У дорозі",
        [DeliveryStatus.Delivered] = "Доставлено",
        [DeliveryStatus.Failed] = "Не вручено",
    };

    [Fact]
    public async Task An_empty_timeline_says_it_is_empty_rather_than_rendering_nothing()
    {
        // FR-84's rule reaching the panel: a blank rectangle is indistinguishable from a panel that
        // failed to load, and a delivery nobody has touched yet is the ordinary case.
        var html = await RenderAsync([]);

        Assert.Contains("Історія цієї доставки поки порожня.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_names_both_statuses_with_the_localized_labels()
    {
        // FR-105: the line reads as a move rather than as a destination, and both ends are the
        // shared status vocabulary rather than a second copy of it.
        var html = await RenderAsync([Change("Тарас Шевченко", UserRole.Dispatcher)]);

        Assert.Contains("Очікує", html, StringComparison.Ordinal);
        Assert.Contains("У дорозі", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_note_only_entry_renders_its_note_and_no_status()
    {
        // FR-107: an entry may carry only a note, and the panel must not invent a transition for it.
        // Read from the list alone: the dialog's head carries the delivery's current status.
        var html = SharedMarkup.ElementWithClass(
            await RenderAsync([Note("Залишено на рецепції")]),
            "ol",
            "dt-timeline-list");

        Assert.Contains("Залишено на рецепції", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Очікує", html, StringComparison.Ordinal);
        Assert.DoesNotContain("У дорозі", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_withheld_actor_shows_a_role_and_no_personal_name()
    {
        // FR-27 and FR-96 reaching the timeline. The service decided; this asserts the panel renders
        // that decision rather than quietly dropping the actor altogether, which would make the
        // history read as though nobody had done anything.
        var html = await RenderAsync([Change(actorName: null, UserRole.Dispatcher)]);

        Assert.DoesNotContain("Шевченко", html, StringComparison.Ordinal);
        Assert.Contains("Диспетчер", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_disclosed_actor_shows_the_name_and_the_role_badge_beside_it()
    {
        var html = await RenderAsync([Change("Тарас Шевченко", UserRole.Dispatcher)]);

        Assert.Contains("Тарас Шевченко", html, StringComparison.Ordinal);

        // The role rides along even when the name was disclosed, which is the half a guard used to
        // suppress: "Тарас Шевченко" alone says who moved the delivery and not what they are, and a
        // reader comparing a driver's move with a dispatcher's has nothing to compare. A Badge
        // rather than a StatusLabel, because a claim about a person never wears the chrome a
        // delivery's status does.
        var actor = SharedMarkup.ElementWithClass(html, "div", "dt-timeline-meta");

        Assert.Contains("dt-badge--role", actor, StringComparison.Ordinal);
        Assert.Contains("Диспетчер", SharedMarkup.TextOf(actor), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_role_a_snapshot_can_hold_has_a_label()
    {
        // Four independent @if blocks and four literal keys, so a role added to the enum without a
        // label renders an anonymous line rather than failing loudly. Walked rather than listed as
        // theory data, so a fifth role is covered here without anyone remembering to add a row -
        // which is the whole claim, and the thing hard-coded rows could not make.
        var roles = Enum.GetValues<UserRole>();

        Assert.NotEmpty(roles);

        foreach (var role in roles)
        {
            var html = await RenderAsync([Change(actorName: null, role)]);

            var actor = SharedMarkup.TextOf(
                SharedMarkup.ElementWithClass(html, "div", "dt-timeline-meta"));

            Assert.True(
                SharedMarkup.IsUkrainian(actor),
                $"The entry for role {role} names no actor: it rendered '{actor}'.");
        }
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.InTransit)]
    [InlineData(DeliveryStatus.Failed)]
    public async Task The_select_offers_keep_and_exactly_the_moves_the_lifecycle_allows(DeliveryStatus status)
    {
        // AD-10 read rather than restated: the select offers whatever Delivery.NextStatuses answers,
        // so the screen cannot drift from the rule the service enforces.
        //
        // Which statuses, not just how many. Counting alone would pass a panel that offered the one
        // wrong move from Pending - a driver would choose "Доставлено" and get a 409 for an option
        // the screen drew.
        var expected = Delivery.NextStatuses(status);
        var html = await RenderAsync([], status, allowStatusChange: true);

        var options = Options(SharedMarkup.ElementWithClass(html, "select", "dt-timeline-next-status"));

        // The first option is "keep": an empty value, naming the status the delivery stays in.
        Assert.Equal(string.Empty, options[0].Value);
        Assert.Contains(Labels[status], options[0].Text, StringComparison.Ordinal);

        var moves = options.Skip(1).ToArray();

        Assert.Equal(expected.Count, moves.Length);

        foreach (var offered in Enum.GetValues<DeliveryStatus>())
        {
            Assert.Equal(
                expected.Contains(offered),
                moves.Any(option => option.Value == offered.ToString() && option.Text == Labels[offered]));
        }

        // And the hint under it names the same moves from the same status.
        var hint = SharedMarkup.TextOf(SharedMarkup.ElementWithClass(html, "div", "dt-timeline-move-hint"));

        Assert.Contains(Labels[status], hint, StringComparison.Ordinal);

        foreach (var offered in expected)
        {
            Assert.Contains(Labels[offered], hint, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_terminal_delivery_offers_no_select_and_no_hint_but_still_the_comment_box()
    {
        // The I/O matrix's terminal row: Delivered has no move, so a select whose only option is
        // "keep" would be a control with nothing to decide. The comment and the submit stay,
        // because FR-107's note is open whatever the status.
        var html = await RenderAsync([], DeliveryStatus.Delivered, allowStatusChange: true);

        Assert.DoesNotContain("dt-timeline-next-status", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-timeline-move-hint", html, StringComparison.Ordinal);
        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("dt-timeline-submit", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_select_follows_the_history_rather_than_the_status_the_panel_opened_with()
    {
        // The refusal path, at the surface a viewer sees it. A status change is refused with a 409
        // when somebody else moved the delivery first, and the panel answers by reloading; if that
        // reload replaced only the list, the select would still offer the move that was just
        // rejected and every retry would fail identically with nothing on screen having changed.
        //
        // Static rendering cannot press a button, but it can arrange the same state: a panel opened
        // on Pending whose history ends at InTransit is exactly what the reload leaves behind.
        var html = await RenderAsync(
            [Change(actorName: null, UserRole.Dispatcher)],
            DeliveryStatus.Pending,
            allowStatusChange: true);

        var moves = Options(SharedMarkup.ElementWithClass(html, "select", "dt-timeline-next-status"))
            .Skip(1)
            .Select(option => option.Value)
            .ToArray();

        Assert.Equal(
            Delivery.NextStatuses(DeliveryStatus.InTransit).Select(status => status.ToString()).ToArray(),
            moves);

        // And the head's status label moved with it.
        var head = SharedMarkup.ElementWithClass(html, "span", "dt-timeline-current");

        Assert.Contains(Labels[DeliveryStatus.InTransit], SharedMarkup.TextOf(head), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_dialog_has_the_artboards_structure()
    {
        // The acceptance criterion: head (title, route line, status, dismiss), History, Add to the
        // timeline, and a footer holding Cancel and the one submit.
        var html = await RenderAsync(
            [Change("Тарас Шевченко", UserRole.Dispatcher)],
            DeliveryStatus.Pending,
            allowStatusChange: true,
            route: true);

        var head = SharedMarkup.ElementWithClass(html, "div", "dt-dialog-heading");

        Assert.Contains("Історія доставки", head, StringComparison.Ordinal);

        var route = SharedMarkup.TextOf(SharedMarkup.ElementWithClass(html, "p", "dt-dialog-subtitle"));

        Assert.Contains("вул. Антоновича, 50", route, StringComparison.Ordinal);
        Assert.Contains("→", route, StringComparison.Ordinal);
        Assert.Contains("вул. Хрещатик, 22", route, StringComparison.Ordinal);
        Assert.Contains("1 коробка", route, StringComparison.Ordinal);
        Assert.Contains("кг", route, StringComparison.Ordinal);

        Assert.Contains("dt-timeline-current", html, StringComparison.Ordinal);
        Assert.Contains("dt-dialog-dismiss", html, StringComparison.Ordinal);
        Assert.Contains("dt-dialog-medium", html, StringComparison.Ordinal);

        Assert.Contains(">Історія</h3>", html, StringComparison.Ordinal);
        Assert.Contains(">Додати до історії</h3>", html, StringComparison.Ordinal);

        // The status comes first, the comment after it, in the compose section.
        Assert.True(
            html.IndexOf("dt-timeline-next-status", StringComparison.Ordinal)
                < html.IndexOf("<textarea", StringComparison.Ordinal),
            "The status select is not drawn above the comment box.");

        var footer = SharedMarkup.ElementWithClass(html, "div", "dt-dialog-actions");

        Assert.Equal(1, SharedMarkup.Occurrences(footer, "dt-timeline-submit"));
        Assert.Equal(1, SharedMarkup.Occurrences(footer, "dt-timeline-cancel"));
        Assert.Contains("Скасувати", footer, StringComparison.Ordinal);

        // One submit: no per-status buttons survive anywhere in the panel. Three buttons in all -
        // the head's dismiss, the submit and Cancel.
        Assert.Equal(3, SharedMarkup.Occurrences(html, "<button"));
    }

    [Fact]
    public async Task Only_the_newest_move_tints_its_dot()
    {
        // The dot of the move that put the delivery where it is wears that status's colour; the
        // creation entry before it and the note after it stay muted.
        var html = await RenderAsync(
        [
            new(1, null, UserRole.Dispatcher, null, DeliveryStatus.Pending, null, Noon),
            Change(actorName: null, UserRole.Driver),
            Note("Залишено на рецепції"),
        ]);

        Assert.Equal(1, SharedMarkup.Occurrences(html, "dt-timeline-dot-"));
        Assert.Contains("dt-timeline-dot-transit", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Keep_sends_a_note_carrying_the_comment()
    {
        var command = Web.Components.Pages.DeliveryTimeline.CommandFor(null, "Залишено на рецепції");

        var note = Assert.IsType<AddDeliveryNoteCommand>(command);

        Assert.Equal("Залишено на рецепції", note.Note);
    }

    [Fact]
    public void A_chosen_status_sends_one_change_carrying_the_status_and_the_comment()
    {
        // FR-106: one entry, not a move followed by a separate note.
        var command = Web.Components.Pages.DeliveryTimeline.CommandFor(
            DeliveryStatus.Failed,
            "Нікого немає вдома");

        var change = Assert.IsType<ChangeDeliveryStatusCommand>(command);

        Assert.Equal(DeliveryStatus.Failed, change.Status);
        Assert.Equal("Нікого немає вдома", change.Note);
    }

    [Fact]
    public void A_choice_the_reload_no_longer_offers_falls_back_to_keep()
    {
        // After a 409 the delivery may be somewhere the chosen move does not start from; left
        // selected, the next submit would be refused for a choice the select no longer shows.
        Assert.Null(Web.Components.Pages.DeliveryTimeline.KeptChoice(
            DeliveryStatus.Delivered,
            Delivery.NextStatuses(DeliveryStatus.Failed)));

        Assert.Null(Web.Components.Pages.DeliveryTimeline.KeptChoice(
            DeliveryStatus.InTransit,
            []));
    }

    [Fact]
    public void A_choice_still_offered_is_kept()
    {
        Assert.Equal(
            DeliveryStatus.Failed,
            Web.Components.Pages.DeliveryTimeline.KeptChoice(
                DeliveryStatus.Failed,
                Delivery.NextStatuses(DeliveryStatus.InTransit)));

        Assert.Null(Web.Components.Pages.DeliveryTimeline.KeptChoice(
            null,
            Delivery.NextStatuses(DeliveryStatus.InTransit)));
    }

    [Fact]
    public async Task The_creation_entry_wears_the_create_glyph_and_a_move_still_wears_edit()
    {
        // Compared against the Icon component's own render rather than a transcribed path, so the
        // assertion is about which glyph the entry chose, not about the glyph's geometry.
        var create = await IconAsync(Web.Components.Shared.IconName.Create);
        var edit = await IconAsync(Web.Components.Shared.IconName.Edit);

        Assert.NotEqual(create, edit);

        var created = await DotAsync(null, DeliveryStatus.Pending);
        var moved = await DotAsync(DeliveryStatus.Pending, DeliveryStatus.InTransit);

        Assert.Contains(create, created, StringComparison.Ordinal);
        Assert.Contains(edit, moved, StringComparison.Ordinal);
        Assert.DoesNotContain(create, moved, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending, true)]
    [InlineData(DeliveryStatus.Delivered, false)]
    public async Task The_comment_hint_appears_only_beside_a_status_select(DeliveryStatus status, bool offered)
    {
        // The hint is about changing the status, so a viewer with no change to make - here a
        // delivery already Delivered - is not told about one.
        var html = await RenderAsync([], status, allowStatusChange: true);

        Assert.Equal(offered, html.Contains("dt-timeline-note-hint", StringComparison.Ordinal));
        Assert.Equal(offered, html.Contains("aria-describedby", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_client_is_not_told_about_a_status_change()
    {
        var html = await RenderAsync([], DeliveryStatus.InTransit, allowStatusChange: false);

        Assert.DoesNotContain("dt-timeline-note-hint", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_hint_is_named_by_the_control_it_describes()
    {
        var html = await RenderAsync([], DeliveryStatus.InTransit, allowStatusChange: true);

        foreach (var (control, hint) in new[]
                 {
                     ("select", "dt-timeline-move-hint"),
                     ("textarea", "dt-timeline-note-hint"),
                 })
        {
            var id = Regex.Match(
                    html,
                    @"<div\b[^>]*\bid=""(?<id>[^""]+)""[^>]*\b" + hint + @"\b",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5))
                .Groups["id"].Value;

            Assert.False(string.IsNullOrEmpty(id), $"The {hint} carries no id.");
            Assert.Matches(
                new Regex(
                    "<" + control + @"\b[^>]*aria-describedby=""" + Regex.Escape(id) + @"""",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5)),
                html);
        }
    }

    [Fact]
    public void The_current_entry_is_the_newest_that_moved_the_delivery()
    {
        IReadOnlyList<TimelineEntryView> history =
        [
            Change(actorName: null, UserRole.Dispatcher),
            Note("Залишено на рецепції"),
        ];

        Assert.Equal(0, Web.Components.Pages.DeliveryTimeline.CurrentEntry(history));
        Assert.Equal(-1, Web.Components.Pages.DeliveryTimeline.CurrentEntry([Note("Спробуємо завтра")]));
    }

    [Fact]
    public void A_history_that_moved_nothing_leaves_the_status_where_it_was()
    {
        // The fallback arm, and the reason the walk skips entries rather than reading the last one:
        // FR-107 lets anyone append a note, so the newest entry is routinely one that carries no
        // status at all. Reading it as the current status would move the delivery backwards on
        // screen every time somebody said something about it.
        var notes = new[] { Note("Немає нікого вдома"), Note("Спробуємо завтра") };

        Assert.Equal(
            DeliveryStatus.Failed,
            Web.Components.Pages.DeliveryTimeline.LatestStatus(notes, DeliveryStatus.Failed));

        Assert.Equal(
            DeliveryStatus.Pending,
            Web.Components.Pages.DeliveryTimeline.LatestStatus([], DeliveryStatus.Pending));
    }

    [Fact]
    public void The_newest_entry_that_moved_the_delivery_is_the_one_that_counts()
    {
        // Oldest-first (FR-108), so the walk runs from the end - and it has to pass over the note
        // that was appended after the move to find it.
        IReadOnlyList<TimelineEntryView> history =
        [
            Change(actorName: null, UserRole.Dispatcher),
            new(3, null, UserRole.Driver, DeliveryStatus.InTransit, DeliveryStatus.Failed, null, Noon),
            Note("Залишено на рецепції"),
        ];

        Assert.Equal(
            DeliveryStatus.Failed,
            Web.Components.Pages.DeliveryTimeline.LatestStatus(history, DeliveryStatus.Pending));
    }

    [Fact]
    public async Task Every_status_the_enum_declares_renders_a_label()
    {
        // The other half of AD-10's closed set. Delivery.NextStatuses throws for a status nobody
        // declared a transition for; the label component answers an empty span for one nobody
        // declared a label for, and that empty span reaches both delivery tables and every timeline
        // line. Walking the enum is what makes the two halves fail together.
        var statuses = Enum.GetValues<DeliveryStatus>();

        Assert.NotEmpty(statuses);

        foreach (var status in statuses)
        {
            var html = await ComponentRenderer.RenderAsync<Web.Components.Shared.DeliveryStatusLabel>(
                new Dictionary<string, object?> { ["Status"] = status });

            var label = SharedMarkup.TextOf(html);

            Assert.True(
                SharedMarkup.IsUkrainian(label),
                $"The status {status} renders no label: it rendered '{label}'.");

            // And it is the label this suite reads elsewhere, so the table below cannot drift from
            // the catalogue while both keep passing.
            Assert.Equal(Labels[status], label);
        }
    }

    [Fact]
    public void The_label_table_covers_every_status()
    {
        // Vacuity guard for the two assertions above: a status missing from this table would make
        // them throw rather than fail, and a table that has fallen behind the enum is the thing that
        // would be wrong.
        Assert.Equal(Enum.GetValues<DeliveryStatus>().Length, Labels.Count);
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.InTransit)]
    [InlineData(DeliveryStatus.Failed)]
    public async Task A_viewer_with_no_lifecycle_action_is_offered_no_select(DeliveryStatus status)
    {
        // FR-90 at the component tier: a client sees the history and the note field and nothing to
        // press. It hides rather than decides - IAccessGuard refuses the request either way - but
        // offering an action that could only ever be refused is worse than offering none.
        var html = await RenderAsync([], status, allowStatusChange: false);

        Assert.DoesNotContain("dt-timeline-next-status", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-timeline-move-hint", html, StringComparison.Ordinal);

        // And the comment box and its submit are still there, because FR-107 is open to everyone
        // who can see the delivery. Losing them with the select would be the easy mistake.
        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("dt-timeline-submit", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_the_panel_renders_is_written_in_Latin()
    {
        // NFR-14 on the rendered chrome rather than on the source: a key that failed to resolve
        // hands back its own name, which is Latin here and nowhere else.
        var html = await RenderAsync(
            [Change(actorName: null, UserRole.Driver), Note("Залишено на рецепції")],
            DeliveryStatus.InTransit,
            allowStatusChange: true);

        var texts = SharedMarkup.TextOf(html)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(text => text.Length > 0)
            .ToArray();

        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, SharedMarkup.HasLatinWord);
    }

    private static Task<string> RenderAsync(
        IReadOnlyList<TimelineEntryView> entries,
        DeliveryStatus status = DeliveryStatus.Pending,
        bool allowStatusChange = false,
        bool route = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["DeliveryId"] = 1,
            ["Status"] = status,
            ["AllowStatusChange"] = allowStatusChange,
        };

        if (route)
        {
            parameters["Pickup"] = new LocationView(new MapLocation(50.43, 30.52), "вул. Антоновича, 50");
            parameters["Dropoff"] = new LocationView(new MapLocation(50.45, 30.52), "вул. Хрещатик, 22");
            parameters["PackageDetails"] = "1 коробка";
            parameters["PackageWeightKg"] = 12m;
        }

        return ComponentRenderer.RenderAsync<Web.Components.Pages.DeliveryTimeline>(
            parameters,
            services => services.AddSingleton<IDeliveryService>(new StubTimeline(entries)));
    }

    /// <summary>
    /// The value and the text of every option in a select's inner HTML, in order. An empty value
    /// renders as a bare <c>value</c> attribute, which reads back as the empty string.
    /// </summary>
    private static (string Value, string Text)[] Options(string select) =>
        Regex.Matches(
                select,
                @"<option\b(?<attributes>[^>]*)>(?<text>[^<]*)</option>",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(match => (
                Regex.Match(
                        match.Groups["attributes"].Value,
                        @"\bvalue=""(?<value>[^""]*)""",
                        RegexOptions.None,
                        TimeSpan.FromSeconds(5))
                    .Groups["value"].Value,
                Regex.Replace(match.Groups["text"].Value, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5)).Trim()))
            .ToArray();

    private static Task<string> IconAsync(Web.Components.Shared.IconName name) =>
        ComponentRenderer.RenderAsync<Web.Components.Shared.Icon>(
            new Dictionary<string, object?> { ["Name"] = name });

    /// <summary>The rendered dot of one shared entry.</summary>
    private static async Task<string> DotAsync(DeliveryStatus? previous, DeliveryStatus? next) =>
        SharedMarkup.ElementWithClass(
            await ComponentRenderer.RenderAsync<Web.Components.Shared.DtTimelineEntry>(
                new Dictionary<string, object?>
                {
                    ["PreviousStatus"] = previous,
                    ["NewStatus"] = next,
                    ["When"] = "07.09.2026 12:00",
                }),
            "span",
            "dt-timeline-dot");

    /// <summary>A status change, with the actor disclosed or withheld.</summary>
    private static TimelineEntryView Change(string? actorName, UserRole role) =>
        new(1, actorName, role, DeliveryStatus.Pending, DeliveryStatus.InTransit, null, Noon);

    /// <summary>A note-only entry: neither status, which is the shape FR-107 asks for.</summary>
    private static TimelineEntryView Note(string note) =>
        new(2, null, UserRole.Client, null, null, note, Noon);

    /// <summary>
    /// A delivery capability that answers a fixed timeline and records nothing. Static rendering
    /// dispatches no events, so the two write members are never reached from here.
    /// </summary>
    private sealed class StubTimeline(IReadOnlyList<TimelineEntryView> entries) : IDeliveryService
    {
        public Task<IReadOnlyList<TimelineEntryView>> ListTimelineAsync(
            int id,
            CancellationToken cancellationToken) =>
            Task.FromResult(entries);

        public Task<TimelineEntryView> ChangeStatusAsync(
            int id,
            ChangeDeliveryStatusCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Static rendering dispatches no events.");

        public Task<TimelineEntryView> AddNoteAsync(
            int id,
            AddDeliveryNoteCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Static rendering dispatches no events.");

        public Task<IReadOnlyList<DeliverySummary>> ListAsync(
            ListDeliveriesQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<IReadOnlyList<AssignedDeliverySummary>> ListMineAsync(
            ListDeliveriesQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<DeliverySummary> GetAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<AssignedDeliverySummary> GetMineAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<IReadOnlyList<DeliverySummary>> ListByIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<DeliverySummary> CreateAsync(
            CreateDeliveryCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<AssignedDeliverySummary> RequestAsync(
            RequestDeliveryCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<DeliverySummary> UpdateAsync(
            int id,
            UpdateDeliveryCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");

        public Task<IReadOnlyList<PlaceMatch>> SearchPlacesAsync(
            SearchPlacesQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The panel reads only the timeline.");
    }
}
