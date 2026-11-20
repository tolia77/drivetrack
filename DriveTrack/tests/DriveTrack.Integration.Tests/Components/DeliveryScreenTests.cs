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

/// <summary>
/// What the two delivery screens actually render, with the capabilities stubbed.
/// <para>
/// A source scan cannot make these claims. "The dispatch board offers a create action and the
/// own-deliveries screen does not" and "a location with no address shows its coordinates" are
/// properties of the output, and the files contain all the same words whether they hold or not.
/// </para>
/// <para>
/// Rendering is static — <see cref="ComponentRenderer"/> never reaches <c>OnAfterRenderAsync</c> and
/// dispatches no events. The sort and filter rules are the server's now, and are asserted against
/// a real database in <c>DeliveryListTests</c>; what is asserted here is what the screens ask for
/// and what they draw of the answer.
/// </para>
/// </summary>
public partial class DeliveryScreenTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly Regex HeaderCell = new(
        @"<th\b[^>]*>(?<body>.*?)</th>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex Column = new(
        @"class=""[^""]*\bcol-lg-6\b[^""]*""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Every stem the product gives a picker, and the only four the suite names. A render under one
    /// of them must show no trace of the other three.
    /// </summary>
    private static readonly string[] Stems = ["pickup", "dropoff", "request-pickup", "request-dropoff"];

    /// <summary>
    /// The caption a picker is given in these cases. Deliberately not one of the two the screens
    /// use: a component that captioned its map from its own resources would pass on either of those.
    /// </summary>
    private const string PickerLabel = "Підпис для перевірки";

    /// <summary>One <c>&lt;DtLocationPicker&gt;</c> call and everything it was handed.</summary>
    private static readonly Regex Picker = new(
        @"<DtLocationPicker\b(?<attributes>[^>]*)>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The three sort glyphs as <c>Icon.razor</c> draws them: the stacked pair every sortable
    /// heading wears, and the two single chevrons the sorted one swaps it for.
    /// </summary>
    private const string SortGlyph = "M8 10l4-4 4 4M8 14l4 4 4-4";

    /// <inheritdoc cref="SortGlyph" />
    private const string AscendingGlyph = "M7 14l5-5 5 5";

    /// <inheritdoc cref="SortGlyph" />
    private const string DescendingGlyph = "M7 10l5 5 5-5";

    /// <summary>The one heading that claims a direction, whichever column it turns out to be.</summary>
    private static readonly Regex SortedHeader = new(
        @"<th\b[^>]*aria-sort=""ascending""[^>]*>(?<body>.*?)</th>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>The actions heading, found by the class the table reserves the column with.</summary>
    private static readonly Regex ActionsHeader = new(
        @"<th\b[^>]*class=""[^""]*\bdt-table-actions\b[^""]*""[^>]*>(?<body>.*?)</th>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>One window cell, whatever shape of window it holds.</summary>
    private static readonly Regex WindowCell = new(
        @"<td\b[^>]*class=""[^""]*\bdt-table-window\b[^""]*""[^>]*>(?<body>.*?)</td>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>One merged package cell, whichever of its two halves the row happens to carry.</summary>
    private static readonly Regex PackageCell = new(
        @"<td\b[^>]*class=""[^""]*\bdt-my-delivery-package\b[^""]*""[^>]*>(?<body>.*?)</td>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>The filter panel: a form wearing the card surface.</summary>
    private static readonly Regex FilterPanel = new(
        @"<form\b[^>]*class=""[^""]*\bdt-filters\b[^""]*""[^>]*>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>The heading row: the screen's title and the action that opens an empty form.</summary>
    private static readonly Regex PageHead = new(
        @"<div\b[^>]*class=""[^""]*\bdt-page-head\b[^""]*""[^>]*>(?<body>.*?)</div>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex Label = new(
        @"<label\b[^>]*>(?<body>.*?)</label>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Every_column_heading_and_field_label_is_Ukrainian(bool dispatch)
    {
        // Translation, asserted on the rendered chrome rather than on the source: a key that
        // resolved to its own name would read as Latin here and nowhere else. The data cells are
        // excluded on purpose - a coordinate is digits and punctuation by nature.
        var html = dispatch
            ? await RenderDeliveriesAsync(UserRole.Dispatcher)
            : await RenderMyDeliveriesAsync();

        var texts = HeaderCell.Matches(html)
            .Concat(Label.Matches(html))
            .Select(match => SharedMarkup.TextOf(match.Groups["body"].Value))
            .Where(text => text.Length > 0)
            .ToArray();

        Assert.NotEmpty(texts);

        var offenders = texts
            .Where(text => !SharedMarkup.IsUkrainian(text) || SharedMarkup.HasLatinWord(text))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("DeliveriesPage.razor", "Dispatcher,Admin")]
    [InlineData("MyDeliveriesPage.razor", "Driver,Client")]
    public void Each_screen_is_reserved_to_the_roles_that_have_a_use_for_it(string fileName, string roles)
    {
        // The attribute is what routes a wrong role to /access-denied instead of into an error
        // boundary. It is a convenience and not the decision - IAccessGuard settles that,
        // and the API suites assert it - but nothing else pins the attribute, and its absence is
        // invisible in a diff.
        var page = SharedMarkup.ReadComponent("Pages", fileName);

        Assert.Contains(
            $"@attribute [Authorize(Roles = \"{roles}\")]",
            page,
            StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------------------

    private static Task<string> RenderDeliveriesAsync(
        UserRole role,
        IReadOnlyList<DeliverySummary>? board = null,
        StubDeliveryService? service = null) =>
        ComponentRenderer.RenderAsync<Web.Components.Pages.Deliveries.DeliveriesPage>(
            parameters: null,
            services =>
            {
                services.AddSingleton<ICurrentUser>(new StubCaller(role));
                services.AddSingleton<IDeliveryService>(service ?? new StubDeliveryService(board: board));
                services.AddSingleton<IDriverService>(new StubDriverService());
                services.AddSingleton<IVehicleService>(new StubVehicleService());
                services.AddSingleton<IClientAdministrationService>(new StubClientRoster());
            });

    /// <summary>
    /// The dispatch board with its rows, for the phone-layout suite next door. A dispatcher rather
    /// than an administrator, because the row a dispatcher is shown is the smaller of the two and
    /// a wrapper that held the table for one holds it for both.
    /// </summary>
    internal static Task<string> RenderBoardAsync() => RenderDeliveriesAsync(UserRole.Dispatcher);

    /// <summary>
    /// The own-deliveries screen with its rows, for the phone-layout suite next door: this class
    /// already keeps the stubs it is given, and a second set of them would be a second answer to
    /// what the screen reads.
    /// </summary>
    internal static Task<string> RenderOwnDeliveriesAsync() =>
        RenderMyDeliveriesAsync(StubDeliveryService.Assigned);

    private static Task<string> RenderMyDeliveriesAsync(
        IReadOnlyList<AssignedDeliverySummary>? rows = null,
        UserRole role = UserRole.Driver,
        StubDeliveryService? service = null) =>
        ComponentRenderer.RenderAsync<Web.Components.Pages.Deliveries.MyDeliveriesPage>(
            parameters: null,
            services =>
            {
                services.AddSingleton<ICurrentUser>(new StubCaller(role));
                services.AddSingleton<IDeliveryService>(service ?? new StubDeliveryService(rows ?? []));
            });

    private static DateTimeOffset Local(int year, int month, int day) =>
        new DateTimeOffset(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local)).ToUniversalTime();

    /// <summary>The language every screen here renders in, which is what its dates are formatted by.</summary>
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

    /// <summary>
    /// One bound of a window as the board writes it: local time, in the reader's own culture.
    /// Composed rather than written out, so the case says what shape the cell takes and does not
    /// also pin a date format that would fail on a machine in another zone.
    /// </summary>
    private static string Moment(DateTimeOffset instant) =>
        instant.ToLocalTime().ToString("g", Ukrainian);

    /// <summary>The far bound of a same-day window, which needs only its time.</summary>
    private static string Time(DateTimeOffset instant) =>
        instant.ToLocalTime().ToString("t", Ukrainian);

    /// <summary>
    /// A cell's words, with the whitespace its markup was indented with collapsed: what is under
    /// test is what the cell says, not how the Razor file is laid out.
    /// </summary>
    private static string Words(string html) => Regex
        .Replace(SharedMarkup.TextOf(html), @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5))
        .Trim();

    /// <summary>One dispatch row, with every field a filter or an ordering reads.</summary>
    private static DeliverySummary Row(
        int id = 1,
        bool assigned = true,
        string details = "Одна палета",
        string? notes = "Подзвонити",
        DeliveryStatus status = DeliveryStatus.Pending,
        DateTimeOffset? created = null,
        bool overdue = false,
        DateTimeOffset? windowEarliest = null,
        DateTimeOffset? windowLatest = null) =>
        new(
            id,
            assigned ? new DeliveryParty(1, "Тарас Шевченко") : null,
            new DeliveryParty(2, "Олена Петренко"),
            new LocationView(new MapLocation(50.4501, 30.5234), null),
            new LocationView(new MapLocation(49.8397, 24.0297), "Львів, площа Ринок"),
            details,
            12.5m,
            notes,
            windowEarliest,
            windowLatest,
            status,
            created ?? Noon,
            overdue);

    /// <summary>
    /// One own-deliveries row. The counterparties are absent because the type has nowhere to put
    /// them, which is the whole of withholding them at this tier.
    /// </summary>
    private static AssignedDeliverySummary OwnRow(
        int id = 1,
        string details = "Одна палета",
        string? notes = null,
        DeliveryStatus status = DeliveryStatus.Pending,
        DateTimeOffset? windowEarliest = null,
        DateTimeOffset? windowLatest = null) =>
        new(
            id,
            new LocationView(new MapLocation(50.4501, 30.5234), null),
            new LocationView(new MapLocation(49.8397, 24.0297), "Львів, площа Ринок"),
            details,
            12.5m,
            notes,
            windowEarliest,
            windowLatest,
            status,
            Noon,
            false);

    /// <summary>A signed-in caller of a chosen role.</summary>
    private sealed class StubCaller(UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public UserId UserId => new(1);

        public UserRole Role => role;

        public DriverId? DriverId => role == UserRole.Driver ? new DriverId(1) : null;

        public ClientId? ClientId => role == UserRole.Client ? new ClientId(2) : null;
    }

    /// <summary>
    /// The deliveries the two screens read. Writes are not exercised — static rendering dispatches
    /// no events — so they answer rather than record.
    /// </summary>
    /// <param name="assigned">
    /// What the own-deliveries screen reads. Null is none, which is that screen's empty state.
    /// </param>
    /// <param name="board">
    /// What the dispatch board reads. Null is the pair below - one delivery with both parties and a
    /// window that has only its far bound, one with neither party and no window at all. A caller
    /// hands in its own to render a board that is empty, or one whose windows take a shape the pair
    /// does not carry.
    /// </param>
    internal sealed class StubDeliveryService(
        IReadOnlyList<AssignedDeliverySummary>? assigned = null,
        IReadOnlyList<DeliverySummary>? board = null)
        : IDeliveryService
    {
        /// <summary>What the own-deliveries screen renders when it has rows.</summary>
        internal static readonly AssignedDeliverySummary[] Assigned =
        [
            new(
                1,
                new LocationView(new MapLocation(50.4501, 30.5234), null),
                new LocationView(new MapLocation(49.8397, 24.0297), "Львів, площа Ринок"),
                "Одна палета",
                12.5m,
                null,
                null,
                Noon,
                DeliveryStatus.InTransit,
                Noon,
                true),
        ];

        private static readonly DeliverySummary[] Board =
        [
            new(
                1,
                new DeliveryParty(1, "Тарас Шевченко"),
                new DeliveryParty(2, "Олена Петренко"),
                new LocationView(new MapLocation(50.4501, 30.5234), null),
                new LocationView(new MapLocation(49.8397, 24.0297), "Львів, площа Ринок"),
                "Одна палета",
                12.5m,
                "Подзвонити",
                null,
                Noon,
                DeliveryStatus.Pending,
                Noon,
                true),
            new(
                2,
                null,
                null,
                new LocationView(new MapLocation(50.4501, 30.5234), null),
                new LocationView(new MapLocation(49.8397, 24.0297), null),
                "Дві коробки",
                4m,
                null,
                null,
                null,
                DeliveryStatus.Delivered,
                Noon,
                false),
        ];

        /// <summary>Every view the dispatch board asked for, in order.</summary>
        public List<ListDeliveriesQuery> AskedBoard { get; } = [];

        /// <summary>Every view the own-deliveries screen asked for, in order.</summary>
        public List<ListDeliveriesQuery> AskedMine { get; } = [];

        public Task<Paged<DeliverySummary>> ListAsync(
            ListDeliveriesQuery query,
            CancellationToken cancellationToken)
        {
            AskedBoard.Add(query);

            return Task.FromResult(TestPage.Of(board ?? Board, query.Offset, query.Limit));
        }

        public Task<Paged<AssignedDeliverySummary>> ListMineAsync(
            ListDeliveriesQuery query,
            CancellationToken cancellationToken)
        {
            AskedMine.Add(query);

            return Task.FromResult(TestPage.Of(assigned ?? [], query.Offset, query.Limit));
        }

        public Task<IReadOnlyList<AssignedDeliverySummary>> ListAllMineAsync(CancellationToken cancellationToken) =>
            Task.FromResult(assigned ?? []);

        public Task<DeliverySummary> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Board[0]);

        public Task<AssignedDeliverySummary> GetMineAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dispatch board never reads one delivery as its own.");

        public Task<IReadOnlyList<DeliverySummary>> ListByIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dispatch board reads a page, not a set of ids.");

        public Task<DeliverySummary> CreateAsync(
            CreateDeliveryCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Board[0]);

        // The client request, for the reason every write above answers rather than records:
        // static rendering dispatches no events, so nothing here is ever called by a screen test -
        // it exists because widening IDeliveryService widens every implementation of it.
        public Task<AssignedDeliverySummary> RequestAsync(
            RequestDeliveryCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Assigned[0]);

        public Task<DeliverySummary> UpdateAsync(
            int id,
            UpdateDeliveryCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Board[0]);

        public Task DeleteAsync(int id, CancellationToken cancellationToken) => Task.CompletedTask;

        // The three timeline members. Answering rather than recording, for the reason the writes
        // above do: static rendering dispatches no events, so nothing here is ever called by a
        // screen test - they exist because widening IDeliveryService widens every implementation of
        // it, which is the compile-time consequence the stub is here to carry.
        public Task<TimelineEntryView> ChangeStatusAsync(
            int id,
            ChangeDeliveryStatusCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Entry);

        public Task<TimelineEntryView> AddNoteAsync(
            int id,
            AddDeliveryNoteCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Entry);

        public Task<IReadOnlyList<TimelineEntryView>> ListTimelineAsync(
            int id,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TimelineEntryView>>([Entry]);

        // The address search, for the reason the three above are here: static rendering
        // dispatches no events, so no render test can press the search button - the member exists
        // because widening IDeliveryService widens every implementation of it.
        public Task<IReadOnlyList<PlaceMatch>> SearchPlacesAsync(
            SearchPlacesQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlaceMatch>>([]);

        private static TimelineEntryView Entry => new(
            1,
            "Тарас Шевченко",
            UserRole.Dispatcher,
            DeliveryStatus.Pending,
            DeliveryStatus.InTransit,
            null,
            Noon);
    }

    /// <summary>
    /// Two drivers: one holding the vehicle the capacity hint reads, and one holding nothing.
    /// <para>
    /// They differ in their standing as well, because the assignment picker renders it and
    /// the two branches of that are what a single stub cannot exercise: a rated driver shows the
    /// average, an unrated one shows the "no value" text and never a zero.
    /// </para>
    /// </summary>
    private sealed class StubDriverService : IDriverService
    {
        internal static readonly DriverSummary Rated = new(
            new DriverId(1),
            new UserId(10),
            "Тарас",
            "Шевченко",
            "taras@drivetrack.test",
            "ВІ123456",
            5,
            "Рено Мастер",
            "АА1234ВВ",
            Rating: 4.5,
            ReviewCount: 2,

            // On duty, so the picker marks them as such. The pair below is one of each, so
            // both branches of the marker are covered by the one render.
            OnDuty: true);

        internal static readonly DriverSummary Unrated = new(
            new DriverId(2),
            new UserId(11),
            "Олег",
            "Коваль",
            "oleh@drivetrack.test",
            "ВІ654321",
            VehicleId: null,
            VehicleModel: null,
            VehicleLicensePlate: null,
            Rating: null,
            ReviewCount: 0,

            // Off duty, and still offered. The assertion that this driver is in the picker
            // at all is what makes "flag, never block" a property of the screen.
            OnDuty: false);

        private static readonly DriverSummary Driver = Rated;

        public Task<Paged<DriverSummary>> ListAsync(
            ListDriversQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(TestPage.Of<DriverSummary>([Rated, Unrated], query.Offset, query.Limit));

        public Task<IReadOnlyList<DriverSummary>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DriverSummary>>([Rated, Unrated]);

        public Task<DriverSummary> GetAsync(DriverId id, CancellationToken cancellationToken) =>
            Task.FromResult(Driver);

        public Task<DriverSummary> CreateAsync(
            CreateDriverCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Driver);

        public Task<DriverSummary> UpdateAsync(
            DriverId id,
            UpdateDriverCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Driver);

        public Task DeleteAsync(DriverId id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubVehicleService : IVehicleService
    {
        private static readonly VehicleSummary Held =
            new(5, "Рено Мастер", "АА1234ВВ", 1200m, 42_000, new DateOnly(2026, 12, 1));

        public Task<Paged<VehicleSummary>> ListAsync(
            ListVehiclesQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(TestPage.Of<VehicleSummary>([Held], query.Offset, query.Limit));

        public Task<IReadOnlyList<VehicleSummary>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VehicleSummary>>([Held]);

        public Task<IReadOnlyList<VehicleSummary>> ListUnassignedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VehicleSummary>>([]);

        public Task<VehicleSummary> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Held);

        public Task<VehicleSummary> CreateAsync(
            CreateVehicleCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Held);

        public Task<VehicleSummary> UpdateAsync(
            int id,
            UpdateVehicleCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Held);

        public Task DeleteAsync(int id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubClientRoster : IClientAdministrationService
    {
        private static readonly ClientAccount Client = new(
            new UserId(11),
            new ClientId(2),
            "Олена",
            "Петренко",
            "olena@drivetrack.test",
            "+380441234567");

        public Task<Paged<ClientAccount>> ListAsync(
            ListClientsQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(TestPage.Of<ClientAccount>([Client], query.Offset, query.Limit));

        public Task<IReadOnlyList<ClientAccount>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientAccount>>([Client]);

        public Task<ClientAccount> GetAsync(UserId userId, CancellationToken cancellationToken) =>
            Task.FromResult(Client);

        public Task<ClientAccount> UpdateAsync(
            UserId userId,
            UpdateClientCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Client);

        public Task DeleteAsync(UserId userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>The dispatch board with one of its headings within a test's reach.</summary>
    internal sealed class ProbeBoard : Web.Components.Pages.Deliveries.DeliveriesPage
    {
        public static ProbeBoard? Last { get; private set; }

        public Task SortAsync(DeliverySort column) => SortByAsync(column);

        protected override void OnInitialized()
        {
            base.OnInitialized();

            Last = this;
        }
    }
}
