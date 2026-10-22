using System.Text.RegularExpressions;
using DriveTrack.Domain.Identity;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Account;
using DriveTrack.Web.Components.Pages;

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
    /// <summary>The four sections the header's anchors jump to, in the design's order.</summary>
    private static readonly string[] Sections = ["features", "roles", "how", "faq"];

    [Fact]
    public async Task An_anonymous_visitor_is_offered_every_entry_point_the_design_draws()
    {
        var html = await ShellCaller.RenderAsync<Home>(role: null);

        // The header: sign in, and create an account.
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        AssertActionWithIcon(header, "sign-in");
        AssertActionWithIcon(header, "register");

        // The hero: create a client account, and the jump to how it works.
        var hero = Section(html, "top");

        AssertActionWithIcon(hero, "register");
        AssertActionWithIcon(hero, "#how");

        // The closing band: both again.
        var cta = SharedMarkup.ElementWithClass(html, "section", "dt-landing-cta");

        AssertActionWithIcon(cta, "register");
        AssertActionWithIcon(cta, "sign-in");

        // The footer's Account column: both, as words like the rest of that column.
        var footer = SharedMarkup.ElementWithClass(html, "footer", "dt-landing-foot");
        var footerLinks = Hrefs(footer);

        Assert.Contains("sign-in", footerLinks);
        Assert.Contains("register", footerLinks);

        // Four ways to register and three to sign in, and none of them leads into the application.
        var links = Hrefs(html);

        Assert.Equal(4, links.Count(href => href == "register"));
        Assert.Equal(3, links.Count(href => href == "sign-in"));
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
        var html = await ShellCaller.RenderAsync<Home>(role);
        var destination = LandingRoute.For(role);
        var label = Catalogue(labelKey);

        // The header's two account buttons become the person and one button into the application.
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");
        Assert.Contains("dt-landing-me", header, StringComparison.Ordinal);
        Assert.Matches(@"class=""dt-landing-avatar""[^>]*>ІБ</span>", header);
        Assert.Contains("Ірина Бондар", header, StringComparison.Ordinal);

        var headerWayIn = AssertActionWithIcon(header, destination);

        Assert.Equal(label, SharedMarkup.TextOf(headerWayIn));

        // The hero's primary action is the same way in.
        var heroWayIn = AssertActionWithIcon(Section(html, "top"), destination);

        Assert.Equal(label, SharedMarkup.TextOf(heroWayIn));

        // And so is the footer's Account column.
        var footer = SharedMarkup.ElementWithClass(html, "footer", "dt-landing-foot");

        Assert.Contains(destination, Hrefs(footer));

        // No sign-in or register entry point anywhere, and no closing band, which exists only to
        // sign somebody up.
        var links = Hrefs(html);

        Assert.DoesNotContain("sign-in", links);
        Assert.DoesNotContain("register", links);
        Assert.DoesNotContain("dt-landing-cta", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Driver)]
    public async Task A_caller_whose_profile_cannot_be_read_keeps_the_way_in_and_loses_only_the_name(
        UserRole role)
    {
        var html = await ShellCaller.RenderAsync<Home>(role, profileFails: true);
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-landing-header");

        AssertActionWithIcon(header, LandingRoute.For(role));
        Assert.DoesNotContain("dt-landing-me", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_landing_page_stands_in_its_own_frame_without_the_sidebar()
    {
        // The design draws a public page with its own header and footer. Home names its own layout,
        // and that layout carries the page and the connection dock - which belongs to the circuit,
        // not to any screen - and no navigation shell.
        var home = SharedMarkup.ReadComponent("Pages", "Home.razor");

        Assert.Contains("@layout LandingLayout", home, StringComparison.Ordinal);

        // FR-13: the page's failures pass through the same boundary every other screen's do, and
        // the dock sits outside it, as MainLayout has it. Read off the source: the boundary is the
        // framework's ErrorBoundary, which this harness cannot render without its logger.
        var layout = SharedMarkup.ReadComponent("Layout", "LandingLayout.razor");
        var markup = Regex.Replace(layout, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.Matches(@"<SessionExpiryBoundary>\s*@Body\s*</SessionExpiryBoundary>\s*<DtConnectionDock />", markup);
        Assert.DoesNotContain("NavMenu", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-shell", markup, StringComparison.Ordinal);

        // And the page itself draws no shell of its own: the brand borrows the shell's mark, which
        // is the only piece of it the landing page wears.
        var html = await ShellCaller.RenderAsync<Home>(role: null);

        Assert.DoesNotMatch(@"class=""dt-nav[\s""]", html);
        Assert.Contains("dt-nav__mark", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_header_anchor_has_a_section_to_land_on_in_the_design_s_order()
    {
        var html = await ShellCaller.RenderAsync<Home>(role: null);
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
        var html = await ShellCaller.RenderAsync<Home>(role);

        var forms = Regex.Matches(
                html,
                @"<form\b[^>]*class=""dt-lang""[^>]*>(?<body>.*?)</form>",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5))
            .ToArray();

        // One in the header, on white, and one in the footer, on the dark band.
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

        Assert.Contains("dt-lang__select--light", header, StringComparison.Ordinal);
        Assert.Contains("dt-lang__select", footer, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-lang__select--light", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void On_a_phone_the_section_anchors_go_and_every_grid_is_one_column()
    {
        var css = SharedMarkup.ReadComponent("Pages", "Home.razor.css");
        var phone = css[css.IndexOf("@media (max-width: 640.98px)", StringComparison.Ordinal)..];

        Assert.Matches(@"\.dt-landing-sections\s*\{\s*display:\s*none;", phone);

        foreach (var grid in new[]
                 {
                     ".dt-landing-hero", ".dt-landing-grid--2", ".dt-landing-grid--3",
                     ".dt-landing-grid--4", ".dt-landing-foot-grid",
                 })
        {
            Assert.Matches(
                Regex.Escape(grid) + @"[^{]*\{\s*grid-template-columns:\s*minmax\(0,\s*1fr\);",
                phone);
        }
    }

    /// <summary>
    /// The link to <paramref name="href"/> inside <paramref name="html"/>, asserted to carry a glyph
    /// and localized words (NFR-24); returns its inner markup.
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
