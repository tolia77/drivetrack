using System.Globalization;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Dashboard;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using DriveTrack.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using DashboardScreen = DriveTrack.Web.Components.Pages.Dashboard;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The dashboard screen over a stubbed service: what it draws for a given view, and what it draws
/// when the view cannot be read. Which rows each figure counts is <c>DashboardServiceTests</c>'s.
/// </summary>
public class DashboardScreenTests
{
    /// <summary>9:15 in Kyiv: a morning, by the local hour the view carries.</summary>
    private static readonly DateTimeOffset Morning = new(2026, 10, 22, 9, 15, 0, TimeSpan.FromHours(3));

    [Fact]
    public async Task A_dispatcher_on_a_busy_day_sees_the_figures_the_warning_and_every_card()
    {
        var html = await RenderAsync(UserRole.Dispatcher, Busy());
        var text = SharedMarkup.TextOf(html);

        Assert.Contains("Доброго ранку, Ірина", text, StringComparison.Ordinal);
        Assert.Contains("відкритих доставок: 18", text, StringComparison.Ordinal);
        Assert.Contains("водіїв на зміні: 4", text, StringComparison.Ordinal);

        // The four tiles, each with its own sentence.
        Assert.Contains("Очікує: 7 · У дорозі: 11", text, StringComparison.Ordinal);
        Assert.Contains("З підтвердженням доставки: 22", text, StringComparison.Ordinal);
        Assert.Contains("4 / 7", text, StringComparison.Ordinal);
        Assert.Contains("Перша зміна відкрита", text, StringComparison.Ordinal);

        // The warning, with both figures and the advice under it.
        var warning = SharedMarkup.ElementWithClass(html, "div", "dt-banner--warning");
        Assert.Contains("Прострочено доставок: 3. Доставок без водія: 2.", SharedMarkup.TextOf(warning), StringComparison.Ordinal);
        Assert.Contains("Призначте водія", SharedMarkup.TextOf(warning), StringComparison.Ordinal);

        // The two actions, each an icon and its words (NFR-24).
        Assert.Contains(@"href=""chat""", html, StringComparison.Ordinal);
        Assert.Contains(@"href=""deliveries""", html, StringComparison.Ordinal);
        Assert.Contains("Створити доставку", text, StringComparison.Ordinal);

        // Today's card: how many of the open deliveries it shows, the row's driver - or the
        // absence of one in words - and the overdue mark beside the status.
        Assert.Contains("Найближчі 2 з 18", text, StringComparison.Ordinal);
        Assert.Contains("вул. Хрещатик, 22", text, StringComparison.Ordinal);
        Assert.Contains("Без водія", text, StringComparison.Ordinal);
        Assert.Contains("dt-badge--overdue", html, StringComparison.Ordinal);

        // The rest of the cards.
        Assert.Contains("dt-dashboard-chat", html, StringComparison.Ordinal);
        Assert.Contains("Буду о 11:05", text, StringComparison.Ordinal);
        Assert.Contains("Renault Master · AA 1234 BK", text, StringComparison.Ordinal);
        Assert.Contains("Без автомобіля", text, StringComparison.Ordinal);
        Assert.Contains("Немає оцінок", text, StringComparison.Ordinal);
        Assert.Contains("Андрій Шевчук про Тараса", text, StringComparison.Ordinal);
        Assert.Contains("Коментар", text, StringComparison.Ordinal);
        Assert.Contains("У дорозі → Доставлено", text, StringComparison.Ordinal);
        Assert.Contains("Наступне ТО", text, StringComparison.Ordinal);

        Assert.False(SharedMarkup.HasLatinWord(Visible(text)), text);
    }

    [Fact]
    public async Task Nothing_overdue_and_nothing_unassigned_draws_no_warning()
    {
        var html = await RenderAsync(UserRole.Dispatcher, Busy() with { Overdue = 0, UnassignedOpen = 0 });

        Assert.DoesNotContain("dt-banner--warning", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Призначте водія", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 0, "Overdue deliveries: 1.", "without a driver")]
    [InlineData(0, 1, "Deliveries without a driver: 1.", "Overdue deliveries")]
    public async Task Either_count_alone_draws_the_warning_with_only_its_own_clause(
        int overdue,
        int unassigned,
        string said,
        string unsaid)
    {
        var html = await RenderAsync(
            UserRole.Dispatcher,
            Busy() with { Overdue = overdue, UnassignedOpen = unassigned },
            culture: CultureInfo.GetCultureInfo("en-US"));

        var warning = SharedMarkup.TextOf(SharedMarkup.ElementWithClass(html, "div", "dt-banner--warning"));

        Assert.Contains(said, warning, StringComparison.Ordinal);
        Assert.DoesNotContain(unsaid, warning, StringComparison.Ordinal);
        Assert.DoesNotContain(": 0", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_of_anything_reads_correctly_in_English()
    {
        var html = await RenderAsync(
            UserRole.Dispatcher,
            Busy() with { Pending = 1, InTransit = 0, DriversOnDuty = 1 },
            culture: CultureInfo.GetCultureInfo("en-US"));

        Assert.Contains(
            "Open deliveries: 1 · Drivers on duty: 1",
            SharedMarkup.TextOf(html),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_long_lists_stack_in_the_wide_column_and_the_short_cards_in_the_narrow_one()
    {
        // Two stacks rather than rows of pairs: a row is as tall as its taller card, which left a
        // hole under whichever card of a pair was short. Stated as document order because each
        // column is a plain stack - the wide one opens first and closes before the narrow one opens.
        var html = await RenderAsync(UserRole.Dispatcher, Busy());

        string[] order =
        [
            "dt-dashboard__column--main",
            "dt-dashboard-today",
            "dt-dashboard-activity",
            @"class=""dt-dashboard__column""",
            "dt-dashboard-drivers",
            "dt-dashboard-chat",
            "dt-dashboard-vehicles",
            "dt-dashboard-reviews",
        ];
        var at = order.Select(marker => html.IndexOf(marker, StringComparison.Ordinal)).ToArray();

        Assert.DoesNotContain(-1, at);
        Assert.Equal(at.Order(), at);
    }

    [Fact]
    public async Task A_day_with_nothing_starting_shows_no_count_beside_the_sentence_that_says_so()
    {
        var html = await RenderAsync(UserRole.Dispatcher, Busy() with { Today = [] });
        var text = SharedMarkup.TextOf(html);

        Assert.Contains("Сьогодні не починається вікно жодної відкритої доставки.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Найближчі", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_administrator_has_no_conversations_card()
    {
        var html = await RenderAsync(UserRole.Admin, Busy() with { Conversations = null });

        Assert.DoesNotContain(@"href=""chat""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-dashboard-conversations", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-dashboard-chat", html, StringComparison.Ordinal);

        // Everything else is still there.
        Assert.Contains("dt-dashboard-activity", html, StringComparison.Ordinal);
        Assert.Contains("Створити доставку", SharedMarkup.TextOf(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_system_says_so_card_by_card()
    {
        var html = await RenderAsync(UserRole.Dispatcher, Empty());
        var text = SharedMarkup.TextOf(html);

        Assert.DoesNotContain("dt-banner--warning", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Перша зміна відкрита", text, StringComparison.Ordinal);
        Assert.Contains("0 / 0", text, StringComparison.Ordinal);

        Assert.Contains("Сьогодні не починається вікно жодної відкритої доставки.", text, StringComparison.Ordinal);
        Assert.Contains("Ще немає жодного водія.", text, StringComparison.Ordinal);
        Assert.Contains("Жодна доставка ще не має історії.", text, StringComparison.Ordinal);
        Assert.Contains("Повідомлень ще немає.", text, StringComparison.Ordinal);
        Assert.Contains("Ще немає жодного автомобіля.", text, StringComparison.Ordinal);
        Assert.Contains("Відгуків поки немає.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_read_shows_the_failure_banner_and_no_figures()
    {
        var html = await RenderAsync(
            UserRole.Dispatcher,
            failure: new ForbiddenException(ErrorCode.AUTH_FORBIDDEN, "refused"));

        Assert.Contains("alert-danger", html, StringComparison.Ordinal);
        Assert.Contains(@"role=""alert""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-dashboard__tiles", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-banner--warning", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(9, "Good morning, Iryna")]
    [InlineData(14, "Good afternoon, Iryna")]
    [InlineData(20, "Good evening, Iryna")]
    public async Task The_greeting_follows_the_local_hour(int hour, string expected)
    {
        var at = new DateTimeOffset(2026, 10, 22, hour, 0, 0, TimeSpan.FromHours(3));

        var html = await RenderAsync(
            UserRole.Dispatcher,
            Busy() with { FirstName = "Iryna", LocalNow = at },
            culture: CultureInfo.GetCultureInfo("en-US"));

        var text = SharedMarkup.TextOf(html);

        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.Contains("Thursday, 22 October 2026 · Open deliveries: 18 · Drivers on duty: 4", text, StringComparison.Ordinal);
        Assert.Contains("7 Pending · 11 In transit", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page's text without what the view itself supplied in Latin letters - a vehicle's model
    /// and plate - so what is left is only what the screen says in its own words.
    /// </summary>
    private static string Visible(string text) =>
        text.Replace("Renault Master", string.Empty, StringComparison.Ordinal)
            .Replace("AA 1234 BK", string.Empty, StringComparison.Ordinal)
            .Replace("KA 5521 CE", string.Empty, StringComparison.Ordinal);

    private static DashboardView Busy() =>
        new(
            "Ірина",
            Morning,
            Pending: 7,
            InTransit: 11,
            Overdue: 3,
            UnassignedOpen: 2,
            DeliveredToday: 24,
            DeliveredTodayWithProof: 22,
            DriversOnDuty: 4,
            DriversTotal: 7,
            FirstOpenShiftStartedAt: Morning.AddHours(-1),
            Today:
            [
                new DashboardDelivery(1, Morning, Morning.AddHours(2), Place("вул. Хрещатик, 22"), "Олена Коваленко", true, DeliveryStatus.InTransit),
                new DashboardDelivery(2, Morning.AddHours(2), Morning.AddHours(4), Place("просп. Перемоги, 5"), null, false, DeliveryStatus.Pending),
            ],
            Drivers:
            [
                new DashboardDriver(new DriverId(1), "Олена Коваленко", "Renault Master", "AA 1234 BK", 4.8, 12, true),
                new DashboardDriver(new DriverId(2), "Михайло Гнатюк", null, null, null, 0, false),
            ],
            Activity:
            [
                new DashboardActivity(1, "Тарас Мельник", UserRole.Driver, DashboardActivityKind.StatusChange, DeliveryStatus.InTransit, DeliveryStatus.Delivered, null, Place("вул. Ділова, 2"), Morning),
                new DashboardActivity(2, "Олена Коваленко", UserRole.Driver, DashboardActivityKind.Comment, null, null, "Клієнт просив зателефонувати", Place("вул. Хрещатик, 22"), Morning.AddMinutes(-20)),
                new DashboardActivity(3, "Ірина Бондар", UserRole.Dispatcher, DashboardActivityKind.Created, null, DeliveryStatus.Pending, null, null, Morning.AddMinutes(-40)),
            ],
            Conversations:
            [
                new DashboardConversation(new DriverId(1), "Олена Коваленко", "Дякую! Буду о 11:05.", Morning.AddMinutes(-10)),
            ],
            Vehicles:
            [
                new DashboardVehicle(1, "Renault Master", "KA 5521 CE", new DateOnly(2026, 10, 25), 48_210),
            ],
            Reviews:
            [
                new DashboardReview(1, 5, "Андрій Шевчук", "Тараса Мельника", "Вчасно і сухо.", Morning.AddHours(-1)),
            ]);

    private static DashboardView Empty() =>
        new(
            "Ірина",
            Morning,
            0, 0, 0, 0, 0, 0, 0, 0,
            null,
            [],
            [],
            [],
            [],
            [],
            []);

    private static LocationView Place(string address) => new(new MapLocation(50.45, 30.52), address);

    private static Task<string> RenderAsync(
        UserRole role,
        DashboardView? view = null,
        Exception? failure = null,
        CultureInfo? culture = null) =>
        ComponentRenderer.RenderAsync<DashboardScreen>(
            parameters: null,
            configureServices: services =>
            {
                services.AddSingleton<ICurrentUser>(new StubCaller(role));
                services.AddSingleton<IDashboardService>(new StubDashboard(view, failure));
            },
            culture);

    private sealed class StubCaller(UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public UserId UserId => new(1);

        public UserRole Role => role;

        public DriverId? DriverId => null;

        public ClientId? ClientId => null;
    }

    private sealed class StubDashboard(DashboardView? view, Exception? failure) : IDashboardService
    {
        public Task<DashboardView> GetAsync(CancellationToken cancellationToken) =>
            failure is not null
                ? Task.FromException<DashboardView>(failure)
                : Task.FromResult(view ?? throw new InvalidOperationException("No view was given."));
    }
}
