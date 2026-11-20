using System.Globalization;
using System.Text.RegularExpressions;
using DriveTrack.Domain.Accounts;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Components.Pages;
using DriveTrack.Web.Components.Routing;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The landing page as the design draws it ("Landing page (public)" and "Landing header · signed
/// in"): a public page with its own header and footer rather than the application's sidebar, and
/// one way in for a caller who already has a session.
/// <para>
/// Rendered per caller rather than read off the source, because the claim is about which entry
/// points each caller is offered, and swapping two guards leaves the file holding the same words.
/// </para>
/// </summary>
public class LandingScreenTests
{
    /// <summary>The three sections the header's anchors jump to, in the design's order.</summary>
    private static readonly string[] Sections = ["features", "how", "faq"];

    [Fact]
    public async Task An_anonymous_visitor_is_offered_every_entry_point_the_design_draws()
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role: null);

        // The header: sign in, and create an account.
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        AssertActionWithIcon(WithoutPhoneMenu(header), "sign-in");
        AssertActionWithIcon(WithoutPhoneMenu(header), "register");

        // The hero: create an account, and sign in.
        var hero = Section(html, "top");

        AssertActionWithIcon(hero, "register");
        AssertActionWithIcon(hero, "sign-in");

        // The closing band: both again.
        var cta = SharedMarkup.ElementWithClass(html, "section", "dt-landing-cta");

        AssertActionWithIcon(cta, "register");
        AssertActionWithIcon(cta, "sign-in");

        // The footer's Account column: both, as words like the rest of that column.
        var footer = SharedMarkup.ElementWithClass(html, "footer", "dt-landing-foot");
        var footerLinks = Hrefs(footer);

        Assert.Contains("sign-in", footerLinks);
        Assert.Contains("register", footerLinks);

        // Five ways to register and five to sign in - the header, the phone's menu that stands in
        // for it on a phone, the hero, the closing band and the footer - and none of them leads
        // into the application.
        var links = Hrefs(html);

        Assert.Equal(5, links.Count(href => href == "register"));
        Assert.Equal(5, links.Count(href => href == "sign-in"));

        // Every other link goes somewhere on this page: no company or legal pages, no contacts.
        Assert.DoesNotContain("#", links);
        Assert.All(
            links.Where(href => href != "register" && href != "sign-in"),
            href => Assert.Contains(href, new[] { "#top", "#features", "#how", "#faq" }));
        Assert.DoesNotContain("/dashboard", links);
        Assert.DoesNotContain("/profile", links);
    }

    [Theory]
    [InlineData(UserRole.Admin, "NavDashboard")]
    [InlineData(UserRole.Dispatcher, "NavDashboard")]
    [InlineData(UserRole.Driver, "Profile")]
    [InlineData(UserRole.Client, "Profile")]
    public async Task A_signed_in_caller_is_shown_who_they_are_and_one_way_in_named_for_where_it_goes(
        UserRole role,
        string labelKey)
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role);
        var destination = LandingRoute.For(role);
        var label = Catalogue(labelKey);

        // The header's two account buttons become the person and one button into the application.
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");
        Assert.Contains("class=\"dt-landing-me\"", header, StringComparison.Ordinal);
        Assert.Matches(@"class=""dt-landing-avatar""[^>]*>ІБ</span>", header);
        Assert.Contains("Ірина Бондар", header, StringComparison.Ordinal);

        var headerWayIn = AssertActionWithIcon(WithoutPhoneMenu(header), destination);

        Assert.Equal(label, SharedMarkup.TextOf(headerWayIn));

        // The hero's primary action is the same way in.
        var heroWayIn = AssertActionWithIcon(Section(html, "top"), destination);

        Assert.Equal(label, SharedMarkup.TextOf(heroWayIn));

        // And so is the footer's Account column.
        var footer = SharedMarkup.ElementWithClass(html, "footer", "dt-landing-foot");

        Assert.Contains(destination, Hrefs(footer));

        // The person opens an account menu: their own profile, and signing out by the POST the
        // navigation uses.
        Assert.Contains("popovertarget=\"dt-landing-account-menu\"", header, StringComparison.Ordinal);

        var accountMenu = Popover(header, "dt-landing-account-menu");
        var myProfile = AssertActionWithIcon(accountMenu, "/profile");

        Assert.Equal(Catalogue("LandingMyProfile"), SharedMarkup.TextOf(myProfile));
        Assert.Matches(@"<form\b[^>]*method=""post""[^>]*action=""/sign-out""", accountMenu);
        Assert.Contains(Catalogue("SignOut"), accountMenu, StringComparison.Ordinal);

        // No sign-in or register entry point anywhere, and no closing band, which exists only to
        // sign somebody up.
        var links = Hrefs(html);

        Assert.DoesNotContain("sign-in", links);
        Assert.DoesNotContain("register", links);

        // And no link to nowhere: everything else is this page's own sections.
        Assert.DoesNotContain("#", links);
        Assert.All(
            links.Where(href => href != destination && href != "/profile"),
            href => Assert.Contains(href, new[] { "#top", "#features", "#how", "#faq" }));
        Assert.DoesNotContain("dt-landing-cta", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Driver)]
    public async Task A_caller_whose_profile_cannot_be_read_keeps_the_way_in_and_loses_only_the_name(
        UserRole role)
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role, profileFails: true);
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        AssertActionWithIcon(WithoutPhoneMenu(header), LandingRoute.For(role));
        Assert.DoesNotContain("class=\"dt-landing-me\"", html, StringComparison.Ordinal);

        // The account menu stays - the way out matters more than the greeting - and its button,
        // with no name to show, is named in words.
        Assert.Contains("popovertarget=\"dt-landing-account-menu\"", header, StringComparison.Ordinal);
        Assert.Contains($"aria-label=\"{Catalogue("LandingAccountMenu")}\"", header, StringComparison.Ordinal);
        Assert.Contains("action=\"/sign-out\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_landing_page_stands_in_its_own_frame_without_the_sidebar()
    {
        // The design draws a public page with its own header and footer. Home names its own layout,
        // and that layout carries the page and the connection dock - which belongs to the circuit,
        // not to any screen - and no navigation shell.
        var home = SharedMarkup.ReadComponent("Pages", "HomePage.razor");

        Assert.Contains("@layout LandingLayout", home, StringComparison.Ordinal);

        // The page's failures pass through the same boundary every other screen's do, and
        // the dock sits outside it, as MainLayout has it. Read off the source: the boundary is the
        // framework's ErrorBoundary, which this harness cannot render without its logger.
        var layout = SharedMarkup.ReadComponent("Layout", "LandingLayout.razor");
        var markup = Regex.Replace(layout, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.Matches(@"<SessionExpiryBoundary>\s*@Body\s*</SessionExpiryBoundary>\s*<DtConnectionDock />", markup);
        Assert.DoesNotContain("NavMenu", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-shell", markup, StringComparison.Ordinal);

        // And the page itself draws no shell of its own: the brand borrows the shell's mark, which
        // is the only piece of it the landing page wears.
        var html = await ShellCaller.RenderAsync<HomePage>(role: null);

        Assert.DoesNotMatch(@"class=""dt-nav[\s""]", html);
        Assert.Contains("dt-nav__mark", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_header_anchor_has_a_section_to_land_on_in_the_design_s_order()
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role: null);
        var header = SharedMarkup.ElementWithClass(html, "nav", "dt-landing-sections");

        Assert.Equal(
            Sections.Select(section => "#" + section).ToArray(),
            Hrefs(header));

        var positions = Sections
            .Select(section =>
            {
                var index = html.IndexOf($"id=\"{section}\"", StringComparison.Ordinal);

                Assert.True(index >= 0, $"The landing page has no section with id '{section}'.");

                return index;
            })
            .ToArray();

        Assert.Equal(positions.Order().ToArray(), positions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UserRole.Client)]
    public async Task Both_language_switches_post_to_the_endpoint_and_come_back_to_the_landing_page(
        UserRole? role)
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role);

        var forms = Regex.Matches(
                html,
                @"<form\b[^>]*class=""dt-lang""[^>]*>(?<body>.*?)</form>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5))
            .ToArray();

        // One in the header, behind its language menu, and one in the footer, on the dark band.
        Assert.Equal(2, forms.Length);

        foreach (var form in forms)
        {
            Assert.Contains("action=\"/set-culture\"", form.Value, StringComparison.Ordinal);
            Assert.Contains("method=\"post\"", form.Value, StringComparison.Ordinal);
            Assert.Contains("name=\"returnUrl\" value=\"/\"", form.Value, StringComparison.Ordinal);
            Assert.Contains("value=\"uk-UA\"", form.Value, StringComparison.Ordinal);
            Assert.Contains("value=\"en-US\"", form.Value, StringComparison.Ordinal);
        }

        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");
        var footer = SharedMarkup.ElementWithClass(html, "footer", "dt-landing-foot");

        // The header draws the switch as a menu: a button naming the current language, opening a
        // popover whose two choices are submit buttons of the form, each carrying its culture.
        Assert.Contains("popovertarget=\"dt-landing-language-menu\"", header, StringComparison.Ordinal);
        Assert.Matches(@"id=""dt-landing-language-menu""[^>]*popover", header);
        Assert.DoesNotContain("<select", header, StringComparison.Ordinal);
        AssertLanguageMenuMarks(
            header, current: "uk-UA", other: "en-US", code: Catalogue("LanguageUkrainian"), language: Catalogue("Language"));

        // The footer keeps the select on the dark band.
        Assert.Contains("dt-lang__select", footer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UserRole.Client)]
    public async Task Under_English_the_language_menu_marks_English_and_its_button_shows_the_English_code(
        UserRole? role)
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role, culture: new CultureInfo("en-US"));
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        AssertLanguageMenuMarks(header, current: "en-US", other: "uk-UA", code: "EN", language: "Language");
    }

    [Fact]
    public void On_a_phone_the_section_anchors_go_and_every_grid_is_one_column()
    {
        var phone = PhoneBlock();

        Assert.Matches(@"\.dt-landing-sections\b[^{]*\{\s*display:\s*none;", phone);

        foreach (var grid in new[]
                 {
                     ".dt-landing-hero", ".dt-landing-faq-row", ".dt-landing-grid--3",
                     ".dt-landing-grid--4",
                 })
        {
            Assert.Matches(
                Regex.Escape(grid) + @"[^{]*\{\s*grid-template-columns:\s*minmax\(0,\s*1fr\);",
                phone);
        }

        // The footer, as the phone artboard draws it: the brand and tagline across the top, the two
        // link columns side by side under it.
        Assert.Matches(
            @"\.dt-landing-foot-grid\s*\{\s*grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\);",
            phone);
        Assert.Matches(@"\.dt-landing-foot-grid > div:first-child\s*\{\s*grid-column:\s*1 / -1;", phone);
    }

    [Fact]
    public async Task The_hero_stands_on_the_design_s_photograph_under_a_header_marked_for_it()
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role: null);

        // The photograph is the hero's first child, so everything after it is drawn over it; it is
        // decoration, and its image says so twice - an empty alternative inside a hidden wrapper.
        Assert.Matches(
            @"<section\b[^>]*id=""top""[^>]*>\s*<div class=""dt-pic-hero-photo""[^>]*aria-hidden=""true""",
            html);

        var photo = SharedMarkup.ElementWithClass(html, "div", "dt-pic-hero-photo");

        // An empty alternative: Blazor writes `alt=""` as the bare attribute, which HTML reads the same.
        Assert.Matches(@"<img\b[^>]*src=""[^""]*images/landing-hero\.jpg""[^>]*\salt(?:="""")?[\s/>]", photo);

        // The theme's hook for the language menu's button, which DtLanguageMenu draws outside this
        // page's scope - and draws in the sign-in frame's white header too, which carries no hook.
        Assert.Matches(@"<header\b[^>]*class=""dt-landing-header dt-landing-header--on-photo""", html);
    }

    [Fact]
    public void The_photograph_ships_with_the_application_and_goes_through_Assets()
    {
        // Served from this application's web root, never hot-linked, and through `@Assets`, so a
        // changed photograph is a changed URL rather than a stale cache.
        var image = new FileInfo(Path.Combine(
            RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "wwwroot", "images", "landing-hero.jpg"));

        Assert.True(image.Exists, "wwwroot/images/landing-hero.jpg is missing.");
        Assert.True(image.Length > 0, "wwwroot/images/landing-hero.jpg is empty.");

        var source = SharedMarkup.ReadComponent("Illustrations", "LandingHeroPhoto.razor");

        Assert.Contains("src=\"@Assets[\"images/landing-hero.jpg\"]\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("http", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_header_is_pinned_and_turned_white_only_where_the_browser_can_drive_it_from_the_scroll()
    {
        var css = WithoutComments(SharedMarkup.ReadComponent("Pages", "HomePage.razor.css"));

        // By default it lies over the top of the page, see-through, and scrolls away with the hero:
        // a browser without scroll-driven animations never shows it see-through over white.
        var header = Regex.Match(
            css,
            @"(?<![-\w])\.dt-landing-header\s*\{(?<body>[^}]*)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(header.Success, "HomePage.razor.css has no .dt-landing-header rule.");
        Assert.Contains("position: absolute;", header.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("background-color: transparent;", header.Groups["body"].Value, StringComparison.Ordinal);

        // Pinned, and recoloured over the header's own height of scroll, only inside the feature
        // query - and nowhere else.
        Assert.Matches(
            @"@supports \(animation-timeline: scroll\(\)\)\s*\{\s*\.dt-landing-header\s*\{[^}]*position:\s*fixed;"
                + @"[^}]*animation:\s*dt-landing-header-to-white\b[^}]*animation-timeline:\s*scroll\(root\);"
                + @"[^}]*animation-range:\s*0 4\.5rem;",
            css);
        Assert.Equal(1, SharedMarkup.Occurrences(css, "position: fixed"));
        Assert.DoesNotContain("position: sticky", css, StringComparison.Ordinal);

        // Where it ends up is today's white header.
        Assert.Matches(
            @"@keyframes dt-landing-header-to-white\s*\{\s*to\s*\{[^}]*--landing-head-ink:\s*var\(--dt-ink\);"
                + @"[^}]*background-color:\s*var\(--dt-surface-card\);",
            css);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Dispatcher)]
    [InlineData(UserRole.Driver)]
    [InlineData(UserRole.Client)]
    public async Task On_a_phone_the_header_s_menu_holds_the_sections_and_the_way_in(UserRole? role)
    {
        var html = await ShellCaller.RenderAsync<HomePage>(role);
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        // The glyph alone, as the phone artboard draws it, and named in words.
        var button = Regex.Match(
            header,
            @"<button\b[^>]*popovertarget=""dt-landing-phone-menu""[^>]*>(?<body>.*?)</button>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(button.Success, $"No phone menu button in:{Environment.NewLine}{header}");
        Assert.Contains($"aria-label=\"{Catalogue("Menu")}\"", button.Value, StringComparison.Ordinal);
        Assert.Contains("<svg", button.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Equal(string.Empty, SharedMarkup.TextOf(button.Groups["body"].Value));

        // The three section anchors in the design's order, then the way in.
        var menu = Popover(header, "dt-landing-phone-menu");
        var links = Hrefs(menu);

        Assert.Equal(Sections.Select(section => "#" + section).ToArray(), links.Take(3).ToArray());

        // A jump within the page is not a click outside the popover, so each section anchor closes
        // the menu itself - or it would stay open over the section it jumped to. Decoded first: the
        // renderer writes the handler's quotes as entities, which the browser reads back.
        var decoded = System.Net.WebUtility.HtmlDecode(menu);

        foreach (var section in Sections)
        {
            Assert.Matches(
                $@"<a\b[^>]*href=""#{section}""[^>]*onclick=""this\.closest\('\[popover\]'\)\.hidePopover\(\)""",
                decoded);
        }

        if (role is null)
        {
            Assert.Equal(["sign-in", "register"], links.Skip(3).ToArray());
            AssertActionWithIcon(menu, "sign-in");
            AssertActionWithIcon(menu, "register");
            Assert.DoesNotContain("/sign-out", menu, StringComparison.Ordinal);
        }

        if (role is { } signedIn)
        {
            var destination = LandingRoute.For(signedIn);

            Assert.Equal([destination, "/profile"], links.Skip(3).ToArray());
            AssertActionWithIcon(menu, destination);
            Assert.Matches(@"<form\b[^>]*method=""post""[^>]*action=""/sign-out""", menu);
            Assert.Contains(Catalogue("SignOut"), menu, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void On_a_phone_the_header_is_one_row_and_the_page_follows_the_phone_artboard()
    {
        var phone = PhoneBlock();

        // Brand, language, menu: every other control leaves the row.
        Assert.Matches(@"\.dt-landing-headrow\s*\{[^}]*flex-wrap:\s*nowrap;", phone);
        Assert.Matches(
            @"\.dt-landing-sections,\s*\.dt-landing-divider,\s*\.dt-landing-account > \.btn\s*\{\s*display:\s*none;",
            phone);

        // The menu buttons are the theme's, with the rest of the menus: the person's leaves the row
        // on a phone and the phone's appears there, and is not drawn above it.
        var theme = Regex.Replace(
            WithoutComments(File.ReadAllText(Path.Combine(
                RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "Styles", "_theme.scss"))),
            @"//[^\n]*",
            " ",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        var themePhone = Regex.Match(
            theme,
            @"(?<wide>\.dt-landing-menu-button--phone\s*\{\s*display:\s*none;\s*\})\s*"
                + @"@media \(max-width: \$dt-breakpoint-phone-max\)\s*\{(?<phone>.*?)\n\}",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(themePhone.Success, "_theme.scss does not hide the phone menu button above the phone breakpoint.");
        Assert.Matches(
            @"\.dt-landing-menu-button--account\s*\{\s*display:\s*none;",
            themePhone.Groups["phone"].Value);
        Assert.Matches(
            @"\.dt-landing-menu-button--phone\s*\{[^}]*display:\s*inline-flex;",
            themePhone.Groups["phone"].Value);

        // The hero's scene and the questions' phone are not drawn; the actions take the full width;
        // the steps' dashed line runs down through the discs.
        Assert.Matches(
            @"\.dt-landing-hero ::deep \.dt-pic-scene,\s*\.dt-landing-faq-row ::deep \.dt-pic-phonewrap\s*\{\s*display:\s*none;",
            phone);
        Assert.Matches(@"\.dt-landing-actions \.btn\s*\{\s*flex:\s*1 1 100%;", phone);
        Assert.Matches(@"\.dt-landing-steps::before\s*\{[^}]*border-top:\s*0;[^}]*border-left:[^;]*dashed", phone);
        Assert.DoesNotMatch(@"\.dt-landing-steps::before\s*\{\s*display:\s*none;", phone);
    }

    /// <summary>The landing page's phone block, comments removed.</summary>
    private static string PhoneBlock()
    {
        var css = WithoutComments(SharedMarkup.ReadComponent("Pages", "HomePage.razor.css"));

        return css[css.IndexOf("@media (max-width: 640.98px)", StringComparison.Ordinal)..];
    }

    /// <summary>A stylesheet's rules, with its comments taken out.</summary>
    private static string WithoutComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The link to <paramref name="href"/> inside <paramref name="html"/>, asserted to carry a glyph
    /// and localized words; returns its inner markup.
    /// </summary>
    private static string AssertActionWithIcon(string html, string href)
    {
        var action = Regex.Match(
            html,
            $"<a[^>]*href=\"{Regex.Escape(href)}\"[^>]*>(?<body>.*?)</a>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(action.Success, $"No link to '{href}' in:{Environment.NewLine}{html}");

        var body = action.Groups["body"].Value;

        Assert.Contains("<svg", body, StringComparison.Ordinal);
        Assert.True(SharedMarkup.IsUkrainian(SharedMarkup.TextOf(body)), $"The '{href}' action reads '{body}'.");

        return body;
    }

    /// <summary>
    /// The header's language menu under one culture: the button shows the current language's code
    /// and names itself with it in the reader's language, and of the two choices only the current one is <c>aria-current</c>.
    /// </summary>
    private static void AssertLanguageMenuMarks(
        string header,
        string current,
        string other,
        string code,
        string language)
    {
        var button = Regex.Match(
            header,
            @"<button\b[^>]*popovertarget=""dt-landing-language-menu""[^>]*>(?<body>.*?)</button>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(button.Success, $"No language menu button in:{Environment.NewLine}{header}");
        Assert.Contains($"aria-label=\"{language}: {code}\"", button.Value, StringComparison.Ordinal);
        Assert.Equal(code, SharedMarkup.TextOf(button.Groups["body"].Value));
        Assert.Contains("<svg", button.Groups["body"].Value, StringComparison.Ordinal);

        var menu = Popover(header, "dt-landing-language-menu");

        // The tick's glyph on both rows: it holds its place on the one not being read. These left
        // Home's source count with the menu, so this is what notices one going missing.
        var choices = Regex.Matches(
            menu,
            @"<button\b[^>]*name=""culture""[^>]*>(?<body>.*?)</button>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.Equal(2, choices.Count);

        foreach (Match row in choices)
        {
            Assert.Contains(
                "<svg",
                SharedMarkup.ElementWithClass(row.Groups["body"].Value, "span", "dt-landing-menu-check"),
                StringComparison.Ordinal);
        }

        Assert.Matches(
            $@"<button[^>]*type=""submit""[^>]*name=""culture""[^>]*value=""{current}""[^>]*aria-current=""true""",
            menu);

        var choice = Regex.Match(
            menu,
            $@"<button[^>]*value=""{other}""[^>]*>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(choice.Success, $"No {other} choice in the language menu.");
        Assert.DoesNotContain("aria-current", choice.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The header without the phone's menu, which holds the same ways in: a header-level assertion
    /// read through this is about the controls drawn in the header row itself, and fails if those go.
    /// </summary>
    private static string WithoutPhoneMenu(string header)
    {
        var stripped = Regex.Replace(
            header,
            @"<div\b[^>]*id=""dt-landing-phone-menu""[^>]*>.*?</div>",
            string.Empty,
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.NotEqual(header, stripped);

        return stripped;
    }

    /// <summary>The inner markup of the popover carrying <paramref name="id"/>.</summary>
    private static string Popover(string html, string id)
    {
        var match = Regex.Match(
            html,
            $@"<div\b[^>]*id=""{Regex.Escape(id)}""[^>]*\bpopover\b[^>]*>(?<body>.*?)</div>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, $"No popover '{id}' in:{Environment.NewLine}{html}");

        return match.Groups["body"].Value;
    }

    /// <summary>The inner markup of the section carrying <paramref name="id"/>.</summary>
    private static string Section(string html, string id)
    {
        var match = Regex.Match(
            html,
            $"<section\\b[^>]*id=\"{Regex.Escape(id)}\"[^>]*>(?<body>.*?)</section>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, $"No section with id '{id}'.");

        return match.Groups["body"].Value;
    }

    /// <summary>Every <c>href</c> in the markup, in order.</summary>
    private static string[] Hrefs(string html) =>
    [
        .. Regex.Matches(html, "href=\"(?<href>[^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups["href"].Value),
    ];

    /// <summary>A value from the Ukrainian catalogue, which every render here is drawn in.</summary>
    private static string Catalogue(string key)
    {
        var catalogue = File.ReadAllText(Path.Combine(
            RepositoryLayout.ProjectDirectory("DriveTrack.Web"),
            "Resources",
            "UiText.resx"));

        var match = Regex.Match(
            catalogue,
            $"<data name=\"{Regex.Escape(key)}\"[^>]*>\\s*<value>(?<value>[^<]*)</value>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, $"UiText.resx has no {key}.");

        return match.Groups["value"].Value;
    }
}
