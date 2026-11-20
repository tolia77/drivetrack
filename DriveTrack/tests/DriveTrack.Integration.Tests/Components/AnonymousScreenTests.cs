using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DriveTrack.Application.Users;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The two screens a visitor reaches before they have an account, read as structure rather than as
/// behaviour.
/// <para>
/// <c>AuthenticationTests</c> already drives both forms end to end through the real POST, and
/// <c>SubmitGuardTests</c> already decides what a guard on them would mean. Neither of them looks
/// at the shape of the page: the frame the design draws - a white top bar and one centred card, with
/// no application sidebar - is pinned here and nowhere else.
/// </para>
/// <para>
/// Rendered over HTTP rather than through <c>ComponentRenderer</c>, because that is the only place
/// these screens exist as themselves: they are <c>[ExcludeFromInteractiveRouting]</c> so their
/// markup really is in the first response, and their <c>EditForm</c>s want the form
/// mapping context only the real endpoint puts behind them.
/// </para>
/// </summary>
public class AnonymousScreenTests(PostgresFixture postgres)
{
    /// <summary>A call to <c>DtCard</c>, with whatever it was handed.</summary>
    private static readonly Regex CardTag = new(
        @"<DtCard\b(?<attributes>[^>]*)>",
        RegexOptions.None,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The rendered half: the route each screen answers on, the wrapper its width is bounded on,
    /// the hook on its submit and the sibling screen it offers a way to. A table rather than two
    /// near-identical runs of assertions, because the claim is precisely that both screens start
    /// the same way.
    /// </summary>
    public static TheoryData<string, string, string, string> Rendered() => new()
    {
        { "/sign-in", "dt-sign-in-panel", "dt-sign-in-submit", "/register" },
        { "/register", "dt-register-panel", "dt-register-submit", "/sign-in" },
    };

    /// <summary>The same two screens by their file under <c>Components/Account</c>.</summary>
    public static TheoryData<string, string, string> Sources() => new()
    {
        { "SignIn", "dt-sign-in-panel", "dt-sign-in-submit" },
        { "Register", "dt-register-panel", "dt-register-submit" },
    };

    /// <summary>Each screen's stylesheet, the wrapper it bounds and the artboard's width for it.</summary>
    public static TheoryData<string, string, string> Stylesheets() => new()
    {
        { "SignIn", "dt-sign-in-panel", "26.25rem" },
        { "Register", "dt-register-panel", "32.5rem" },
    };

    /// <summary>
    /// The copy the artboards draw, per screen and language: the title and the subtitle, the sentence
    /// offering the other form, the link in it, and the submit. Written out rather than read from the
    /// catalogue, because the catalogue is what this holds to the design.
    /// </summary>
    public static TheoryData<string, string, string, string, string, string, string> Copy() => new()
    {
        {
            "/sign-in", "uk-UA", "Вхід", "Увійдіть у свій обліковий запис DriveTrack.",
            "Ще не маєте облікового запису?", "Зареєструватися", "Увійти"
        },
        {
            "/sign-in", "en-US", "Sign in", "Sign in to your DriveTrack account.",
            "No account yet?", "Sign up", "Sign in"
        },
        {
            "/register", "uk-UA", "Створення облікового запису", "Заповніть свої дані, щоб почати користуватися DriveTrack.",
            "Уже маєте обліковий запис?", "Увійти", "Створити акаунт"
        },
        {
            "/register", "en-US", "Create an account", "Fill in your details to start using DriveTrack.",
            "Already have an account?", "Sign in", "Create account"
        },
    };

    /// <summary>
    /// Every box on the two forms and the token a password manager should fill it by. Stated per
    /// box rather than counted, because the failure worth catching is the plausible wrong token -
    /// <c>current-password</c> on a registration box offers the password of whoever last signed in
    /// on this browser, and every count of "six boxes carry one" is satisfied by it.
    /// </summary>
    public static TheoryData<string, string, string> AutocompleteTokens() => new()
    {
        { "/sign-in", "sign-in-email", "username" },
        { "/sign-in", "sign-in-password", "current-password" },
        { "/register", "register-first-name", "given-name" },
        { "/register", "register-last-name", "family-name" },
        { "/register", "register-email", "email" },
        { "/register", "register-phone", "tel" },
        { "/register", "register-password", "new-password" },
        { "/register", "register-password-confirmation", "new-password" },
    };

    [Theory]
    [MemberData(nameof(Rendered))]
    public async Task The_screen_renders_anonymously_as_one_heading_inside_a_titleless_card(
        string route,
        string wrapper,
        string submit,
        string sibling)
    {
        var html = await RenderAsync(route);

        // Exactly one, and it is the screen's own. `FocusOnNavigate Selector="h1"` in Routes.razor
        // moves the keyboard to the first one it finds, so a second is a coin toss about where a
        // visitor lands - and DtCard renders an <h2> precisely so it can never become that second.
        // Counted rather than merely found, because the titleless card is the decision under test:
        // a `Title` added later would put the screen's own name on the page twice.
        Assert.Equal(1, SharedMarkup.Occurrences(html, "<h1"));

        // The wrapper really holds the card, rather than the class appearing somewhere on the
        // page: a closing </div> moved above the card would leave a `Contains` satisfied and the
        // width bound wrapped around nothing.
        var panel = PhoneLayoutTests.BodyOf(html, wrapper);
        var card = PhoneLayoutTests.BodyOf(panel, "dt-card__body");

        // The heading, the subtitle and the form are all inside the card, in that order: the design
        // draws no heading row over it.
        var heading = card.IndexOf("<h1", StringComparison.Ordinal);
        var subtitle = card.IndexOf("dt-auth-panel__subtitle", StringComparison.Ordinal);
        var form = card.IndexOf("<form", StringComparison.Ordinal);

        Assert.True(heading >= 0, $"The card on {route} holds no heading.");
        Assert.True(heading < subtitle, $"The subtitle on {route} is not under the heading.");
        Assert.True(subtitle < form, $"The form on {route} is not under the subtitle.");
        Assert.Contains(
            "dt-muted",
            Regex.Match(card, @"<p\b[^>]*\bdt-auth-panel__subtitle\b[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(5)).Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dt-page-head", html, StringComparison.Ordinal);

        // And the heading is bare. Not because there is no navigation to echo - AccessDenied and
        // NotFound are anonymous too and both carry a glyph - but because those two report a
        // status and the glyph is that status, while this is a form whose content is the boxes
        // below. A glyph here would be decoration.
        Assert.DoesNotContain("<svg", SharedMarkup.ElementWithClass(card, "h1", "dt-type-h1"), StringComparison.Ordinal);

        // No head on the card: DtCard's <h2> would name the screen a second time and cannot stand in
        // for the <h1> in any case.
        Assert.DoesNotContain("dt-card__head", panel, StringComparison.Ordinal);

        // The way to the other form, inside the card under the form. A visitor here has no
        // navigation menu, so this link is the only offered route between the two screens, and it
        // is pinned the way AccessDeniedDeliveryTests pins that page's way out.
        Assert.True(
            card.IndexOf($@"href=""{sibling}""", StringComparison.Ordinal) > card.IndexOf("</form>", StringComparison.Ordinal),
            $"The way from {route} to {sibling} is not in the card under the form.");

        // The action carries a glyph AND its word.
        var button = SharedMarkup.ElementWithClass(html, "button", submit);

        Assert.Contains("<svg", button, StringComparison.Ordinal);
        Assert.True(
            SharedMarkup.IsUkrainian(SharedMarkup.TextOf(WebUtility.HtmlDecode(button))),
            $"The submit on {route} renders a glyph and no word.");
    }

    [Theory]
    [InlineData("/sign-in")]
    [InlineData("/register")]
    public async Task The_screen_stands_in_its_own_frame_with_no_sidebar(string route)
    {
        var html = await RenderAsync(route);

        // Not the application's shell: the dark navigation is what the design took off these two.
        Assert.DoesNotContain("dt-shell", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<nav\b[^>]*\bdt-nav\b", html);

        // The top bar: the shell's lock-up leading to the landing page, and the language menu.
        var header = SharedMarkup.ElementWithClass(html, "header", "dt-auth-header");

        Assert.Matches(@"<a\b[^>]*\bdt-auth-brand\b[^>]*href=""/""", header);
        Assert.Contains("dt-nav__mark", header, StringComparison.Ordinal);
        Assert.Contains(@"popovertarget=""dt-landing-language-menu""", header, StringComparison.Ordinal);

        // The menu comes back to the route it was opened on, as a local address: anything else is
        // refused by the endpoint and sends the reader to the root.
        Assert.Contains($@"name=""returnUrl"" value=""{route}""", header, StringComparison.Ordinal);
        Assert.Contains(@"action=""/set-culture""", header, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", header, StringComparison.Ordinal);

        // No select: this is the white header's menu rather than the dark band's control.
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);

        // The dock, which only LandingLayout draws here: once, so the frame really does nest in it.
        Assert.Equal(1, SharedMarkup.Occurrences(html, @"id=""blazor-error-ui"""));

        // The card is the main region's, centred by the frame.
        Assert.Contains("dt-auth-panel", SharedMarkup.ElementWithClass(html, "main", "dt-auth-main"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_frame_nests_in_the_landing_layout_rather_than_copying_it()
    {
        var layout = SharedMarkup.ReadComponent("Layout", "AuthLayout.razor");
        var markup = Regex.Replace(layout, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        // The boundary and the dock are LandingLayout's, once.
        Assert.Contains("@layout LandingLayout", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionExpiryBoundary", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("DtConnectionDock", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("NavMenu", markup, StringComparison.Ordinal);

        // One form written once: the landing page's header draws the same component, and the rules
        // both wear are the theme's, because a scoped rule could not reach it.
        Assert.Contains(@"<DtLanguageMenu ReturnUrl=""@ReturnUrl"" />", markup, StringComparison.Ordinal);
        Assert.Contains(@"<DtLanguageMenu ReturnUrl=""/"" />", SharedMarkup.ReadComponent("Pages", "Home.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".dt-landing-menu",
            WithoutComments(SharedMarkup.ReadComponent("Pages", "Home.razor.css")),
            StringComparison.Ordinal);
        Assert.Contains(".dt-landing-menu-button", Theme(), StringComparison.Ordinal);

        // The frame as drawn: a 72px bar and the 32px and 16px around the card.
        var css = WithoutComments(SharedMarkup.ReadComponent("Layout", "AuthLayout.razor.css"));

        Assert.Matches(@"\.dt-auth-headrow\s*\{[^}]*min-height:\s*4\.5rem", css);
        Assert.Matches(
            @"\.dt-auth-main\s*\{[^}]*align-items:\s*center;[^}]*justify-content:\s*center;[^}]*padding:\s*var\(--dt-space-6\) var\(--dt-space-4\)",
            css);

        // On a phone the bar gives up its inset and may wrap, so the brand and the menu always fit.
        var phone = css[css.IndexOf("@media (max-width: 640.98px)", StringComparison.Ordinal)..];

        Assert.Matches(@"\.dt-auth-headrow\s*\{[^}]*flex-wrap:\s*wrap;", phone);
        Assert.Matches(@"\.dt-auth-headrow\s*\{[^}]*padding:\s*0;", phone);
    }

    [Fact]
    public void The_panel_is_drawn_as_the_artboards_draw_it()
    {
        var theme = Theme();

        // 32px inside the card, not the `space-4` every other card has.
        Assert.Matches(@"\.dt-auth-panel \.dt-card__body\s*\{[^}]*padding:\s*var\(--dt-space-6\)", theme);

        // 16px between the fields and 24px above the form.
        Assert.Matches(
            @"\.dt-auth-panel form\s*\{[^}]*gap:\s*var\(--dt-space-4\);[^}]*margin-top:\s*var\(--dt-space-5\)",
            theme);

        // 44px controls and a full-width submit.
        Assert.Matches(
            @"\.dt-auth-panel \.form-control,\s*\.dt-auth-panel \.dt-check,\s*\.dt-auth-panel \.btn\s*\{[^}]*min-height:\s*var\(--dt-touch-target\)",
            theme);
        Assert.Matches(@"\.dt-auth-panel \.btn\s*\{[^}]*width:\s*100%", theme);

        // The tick's label no wider than its phrase: stretched, a click in the blank space beside
        // the words would flip whether the session outlives the browser.
        Assert.Matches(@"\.dt-auth-panel \.dt-check\s*\{[^}]*align-self:\s*flex-start", theme);

        // The way to the other form, centred and 24px under it.
        Assert.Matches(
            @"\.dt-auth-panel__switch\s*\{[^}]*margin:\s*var\(--dt-space-5\) 0 0;[^}]*text-align:\s*center",
            theme);

        // The name row's own 12rem floor. `.dt-cols` is 14rem, and two of those plus their gap do not
        // fit the 28.5rem card body, so borrowing it would stack what the artboard sets side by side.
        // Capped at the row's own width, so a body narrower than the floor does not overflow.
        Assert.Matches(
            @"\.dt-auth-panel__names\s*\{[^}]*grid-template-columns:\s*repeat\(auto-fit,\s*minmax\(min\(12rem,\s*100%\),\s*1fr\)\)",
            theme);
    }

    [Fact]
    public async Task First_and_last_name_share_one_row_and_the_placeholder_is_a_number_the_validator_accepts()
    {
        var html = await RenderAsync("/register");

        var row = PhoneLayoutTests.BodyOf(html, "dt-auth-panel__names");

        Assert.Contains(@"id=""register-first-name""", row, StringComparison.Ordinal);
        Assert.Contains(@"id=""register-last-name""", row, StringComparison.Ordinal);
        Assert.DoesNotContain(@"id=""register-email""", row, StringComparison.Ordinal);

        // The artboard draws spaces in this example, and the validator refuses them: somebody who
        // copied the example would be told it was wrong.
        var phone = Box(html, "register-phone");
        var placeholder = Regex.Match(phone, @"placeholder=""(?<value>[^""]*)""", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.True(placeholder.Success, "The phone box carries no placeholder.");
        Assert.Equal(
            Catalogue("UiText.resx", "PhoneNumberPlaceholder"),
            WebUtility.HtmlDecode(placeholder.Groups["value"].Value));

        // In both catalogues, because the English one is read only after a language change.
        Assert.Matches(RegisterClientCommandValidator.PhoneNumberPattern, Catalogue("UiText.resx", "PhoneNumberPlaceholder"));
        Assert.Matches(RegisterClientCommandValidator.PhoneNumberPattern, Catalogue("UiText.en.resx", "PhoneNumberPlaceholder"));

        Assert.Contains(@"type=""tel""", phone, StringComparison.Ordinal);

        // The reference resolves: it names an element on the page, and that element holds the hint.
        var described = Regex.Match(phone, @"aria-describedby=""(?<id>[^""]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.True(described.Success, "The phone box names nothing that describes it.");

        var hint = Regex.Match(
            html,
            @"<(?<tag>\w+)\b[^>]*\bid=""" + Regex.Escape(described.Groups["id"].Value) + @"""[^>]*>(?<body>.*?)</\k<tag>>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(hint.Success, $"No element carries the id '{described.Groups["id"].Value}'.");
        Assert.Equal(Catalogue("UiText.resx", "PhoneNumberHint"), Text(hint.Groups["body"].Value));
    }

    [Theory]
    [InlineData("/sign-in")]
    [InlineData("/register")]
    public async Task No_box_invites_a_browser_bubble_in_front_of_the_servers_field_errors(string route)
    {
        var html = await RenderAsync(route);

        foreach (Match box in Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            Assert.DoesNotMatch(@"\brequired\b", box.Value);
            Assert.DoesNotContain(@"type=""email""", box.Value, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Copy))]
    public async Task The_screen_reads_as_the_artboard_does_in_each_language(
        string route,
        string culture,
        string title,
        string subtitle,
        string otherFormSentence,
        string otherFormLink,
        string submit)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = await ApiFactory.CreateAsync(
            postgres.ConnectionString, cancellationToken, useProbeAuthentication: false);

        // Through the same switch a visitor uses, which this also covers:
        // the choice is honoured and the reader comes back to the screen they were on.
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var opening = await client.GetStringAsync(new Uri(route, UriKind.Relative), cancellationToken);
        var token = Regex.Match(
            SharedMarkup.ElementWithClass(opening, "header", "dt-auth-header"),
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(token.Success, $"The language menu on {route} carries no antiforgery token.");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["culture"] = culture,
            ["returnUrl"] = route,
            ["__RequestVerificationToken"] = token.Groups[1].Value,
        });

        using var switched = await client.PostAsync(new Uri("/set-culture", UriKind.Relative), form, cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, switched.StatusCode);
        Assert.Equal(route, switched.Headers.Location?.OriginalString);

        var html = await client.GetStringAsync(new Uri(route, UriKind.Relative), cancellationToken);

        Assert.Equal(title, Text(SharedMarkup.ElementWithClass(html, "h1", "dt-type-h1")));
        Assert.Equal(subtitle, Text(SharedMarkup.ElementWithClass(html, "p", "dt-auth-panel__subtitle")));
        Assert.Equal(title, Text(SharedMarkup.ElementWithClass(html, "title", string.Empty)));

        var way = SharedMarkup.ElementWithClass(html, "p", "dt-auth-panel__switch");

        Assert.Equal(otherFormSentence + " " + otherFormLink, Text(way));
        Assert.Equal(otherFormLink, Text(SharedMarkup.ElementWithClass(way, "a", string.Empty)));

        var button = SharedMarkup.ElementWithClass(html, "button", "dt-" + route.TrimStart('/') + "-submit");

        Assert.Equal(submit, Text(button));
    }

    [Theory]
    [MemberData(nameof(AutocompleteTokens))]
    public async Task Every_box_on_the_anonymous_forms_names_what_a_password_manager_should_fill_it_with(
        string route,
        string id,
        string token)
    {
        // These two forms are the only ones in the product a password manager is asked to fill:
        // the six boxes behind the signed-in dialogs already carry their token, and these eight
        // were the set that did not. Without one a manager guesses from the surrounding labels,
        // and the guess it makes on a registration form is the password it stored for whoever
        // last signed in on this browser.
        var html = await RenderAsync(route);

        Assert.Contains($@"autocomplete=""{token}""", Box(html, id), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_post_renders_its_refusal_inside_the_panel_it_is_about()
    {
        // The half of these screens no test rendered at all: what a refusal looks like. The
        // banner used to sit above the panel at the full width of the main column, which put a
        // sentence about a 30rem form on a measure belonging to the page - and moving it is a
        // change nothing in the suite could see, because nothing in the suite ever posted a form
        // and then read the page back for its shape.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = await ApiFactory.CreateAsync(
            postgres.ConnectionString, cancellationToken, useProbeAuthentication: false);

        // A refusal that names no field, which is the banner's whole reason for existing:
        // AUTH_INVALID_CREDENTIALS cannot go under either box without telling an attacker which of
        // the two was wrong.
        var refusedSignIn = await PostAsync(
            factory,
            "/sign-in",
            "signIn",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Form.Email"] = "nobody@drivetrack.test",
                ["Form.Password"] = "Wrong-Passw0rd",
            },
            cancellationToken);

        var signInCard = PhoneLayoutTests.BodyOf(
            PhoneLayoutTests.BodyOf(refusedSignIn, "dt-sign-in-panel"),
            "dt-card__body");

        // In the card, between the subtitle and the form it refuses.
        var banner = signInCard.IndexOf("dt-banner__body", StringComparison.Ordinal);

        Assert.True(banner > signInCard.IndexOf("dt-auth-panel__subtitle", StringComparison.Ordinal), "The banner is not under the subtitle.");
        Assert.True(banner < signInCard.IndexOf("<form", StringComparison.Ordinal), "The banner is not above the form.");

        // And the password is not written back into the page.
        Assert.DoesNotContain("value=", Box(refusedSignIn, "sign-in-password"), StringComparison.Ordinal);

        // And a refusal that does name one, which lands under the box it is about rather than in
        // the banner - the same panel, one level further in.
        var refusedRegistration = await PostAsync(
            factory,
            "/register",
            "register",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Form.FirstName"] = "Олена",
                ["Form.LastName"] = "Петренко",
                ["Form.Email"] = Guid.NewGuid().ToString("N")[..12] + "@drivetrack.test",
                ["Form.PhoneNumber"] = "+380441234567",
                ["Form.Password"] = "Passw0rd-Test",
                ["Form.PasswordConfirmation"] = "Something-Else-1",
            },
            cancellationToken);

        var registerCard = PhoneLayoutTests.BodyOf(
            PhoneLayoutTests.BodyOf(refusedRegistration, "dt-register-panel"),
            "dt-card__body");

        Assert.Contains("dt-field-error", registerCard, StringComparison.Ordinal);

        // Neither half of the submitted password is written back into the page.
        Assert.DoesNotContain("value=", Box(refusedRegistration, "register-password"), StringComparison.Ordinal);
        Assert.DoesNotContain("value=", Box(refusedRegistration, "register-password-confirmation"), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void The_screen_stays_statically_rendered_with_a_hooked_submit(
        string file,
        string wrapper,
        string submit)
    {
        var screen = SharedMarkup.ReadComponent("Account", file + ".razor");

        // The key `SubmitGuardTests` reads these two files by, and the only part of that
        // arrangement worth restating here. That class records the exemption *before* it looks for
        // a `disabled` binding, and says so in as many words: ordered the other way, adding a
        // guard - "a harmless change, and a safer one" - would drop the file from an exemption set
        // asserted by equality and fail a test for getting better. So a binding is permitted, and
        // nothing here forbids one. What is not permitted is losing the attribute: without it
        // these become interactive screens with unguarded submits, which that class reports as
        // offenders, and they also fall out of the set - two failures away from one deleted line.
        Assert.Contains("@attribute [ExcludeFromInteractiveRouting]", screen, StringComparison.Ordinal);
        Assert.Contains("@attribute [AllowAnonymous]", screen, StringComparison.Ordinal);
        Assert.Contains("@layout AuthLayout", screen, StringComparison.Ordinal);

        // The hook is on the submit rather than merely somewhere in the file.
        Assert.Matches(
            new Regex(
                @"<button\b[^>]*\b" + Regex.Escape(submit) + @"\b[^>]*type=""submit""",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5)),
            screen);

        // Titleless in the call rather than only in the output. Asserted as the absence of a
        // `Title` rather than as the exact spelling `<DtCard>`: the precedent this follows,
        // `ProofCapture.razor:49`, is `<DtCard Class="dt-proof-summary">` - titleless without
        // being attribute-free - so a hook added here later is a change these screens are allowed
        // to make and an exact-spelling assertion would refuse it.
        var card = CardTag.Match(screen);

        Assert.True(card.Success, $"{file}.razor calls no DtCard.");
        Assert.DoesNotContain("Title", card.Groups["attributes"].Value, StringComparison.Ordinal);

        // And the rule in the stylesheet has something to hang from. A scoped selector whose
        // element was renamed matches nothing and reports nothing.
        Assert.Contains(wrapper, screen, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Stylesheets))]
    public void The_panel_is_bounded_at_the_artboards_width(
        string file,
        string wrapper,
        string width)
    {
        var css = WithoutComments(SharedMarkup.ReadComponent("Account", file + ".razor.css"));

        // `max-width` rather than `width`, which is what lets the panel fill a viewport narrower
        // than the bound instead of overflowing it. A figure rather than a token: the token scan
        // only refuses the figures the palette names, and this is one screen's geometry.
        Assert.Matches(
            new Regex(
                @"\." + Regex.Escape(wrapper) + @"\s*\{[^}]*max-width:\s*" + Regex.Escape(width),
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5)),
            css);

        // No `::deep`, and the absence is the claim rather than an omission. The bound is on this
        // screen's own wrapper; reached through `::deep` onto DtCard's <div> it would be one
        // component deciding how wide another component's element is.
        //
        // Read off the declarations rather than the file, because the file explains itself: both
        // stylesheets say in prose why they do not reach into the card, and a scan that counted
        // that sentence would fail a stylesheet for stating its own reasoning.
        Assert.DoesNotContain("::deep", css, StringComparison.Ordinal);
    }

    /// <summary>A stylesheet with its <c>/* … */</c> comments removed.</summary>
    private static string WithoutComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary><c>_theme.scss</c> with its line comments removed.</summary>
    private static string Theme() =>
        Regex.Replace(
            File.ReadAllText(Path.Combine(RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "Styles", "_theme.scss")),
            @"//[^\n]*",
            " ",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    /// <summary>One value from a catalogue under <c>Resources</c>.</summary>
    private static string Catalogue(string fileName, string key) =>
        XDocument.Load(Path.Combine(RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "Resources", fileName))
            .Root!
            .Elements("data")
            .Single(element => element.Attribute("name")?.Value == key)
            .Element("value")!
            .Value;

    /// <summary>The opening tag of the box with this id.</summary>
    private static string Box(string html, string id)
    {
        var box = Regex.Match(
            html,
            @"<input\b[^>]*\bid=""" + Regex.Escape(id) + @"""[^>]*>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(box.Success, $"The page renders no box with id '{id}'.");

        return box.Value;
    }

    /// <summary>What a reader reads in a piece of markup, with its whitespace settled.</summary>
    private static string Text(string html) =>
        Regex.Replace(
            WebUtility.HtmlDecode(SharedMarkup.TextOf(html)),
            @"\s+",
            " ",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)).Trim();

    /// <summary>One of the two screens, fetched anonymously over the real pipeline.</summary>
    /// <remarks>
    /// The real cookie scheme rather than the probe, because "anonymously" is the whole claim: a
    /// host whose default scheme hands every request a principal cannot tell a page that allows
    /// anonymous callers from one that merely never asked.
    /// </remarks>
    private async Task<string> RenderAsync(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = await ApiFactory.CreateAsync(
            postgres.ConnectionString, cancellationToken, useProbeAuthentication: false);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Posts one of the forms and answers the page it re-rendered. Its own copy rather than
    /// <c>AuthenticationTests</c>' - that class keeps a reader for the cookie and the redirect,
    /// which is the half this one has no opinion about.
    /// </summary>
    private static async Task<string> PostAsync(
        ApiFactory factory,
        string route,
        string handler,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        using var page = await client.GetAsync(new Uri(route, UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var html = await page.Content.ReadAsStringAsync(cancellationToken);

        var token = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(token.Success, $"{route} rendered no antiforgery token.");

        var cookies = page.Headers.TryGetValues("Set-Cookie", out var headers)
            ? string.Join("; ", headers.Select(value => value.Split(';')[0]))
            : string.Empty;

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>(fields, StringComparer.Ordinal)
            {
                ["_handler"] = handler,
                ["__RequestVerificationToken"] = token.Groups[1].Value,
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
        {
            Content = form,
        };

        if (cookies.Length > 0)
        {
            request.Headers.Add("Cookie", cookies);
        }

        using var response = await client.SendAsync(request, cancellationToken);

        // The refusal is re-rendered in place, not redirected to.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
