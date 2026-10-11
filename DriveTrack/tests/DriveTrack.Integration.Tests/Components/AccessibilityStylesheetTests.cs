using System.Text.RegularExpressions;
using DriveTrack.Integration.Tests.Support;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The stylesheet rules that only take effect under a user's own system setting - forced colours,
/// reduced motion - and so are invisible in every render and every default-settings screenshot.
/// <para>
/// Read as source, in the style <see cref="PhoneLayoutTests"/> reads the phone block: nothing in
/// this solution can switch a browser into high contrast or reduced motion, and a rule that silently
/// stops matching under one of them is exactly the regression nobody on the team would see.
/// </para>
/// </summary>
public class AccessibilityStylesheetTests
{
    [Fact]
    public void Status_markers_paint_in_the_system_text_colour_under_forced_colours()
    {
        // Forced colours replace every background with the system canvas, and the status markers
        // are painted as backgrounds - so without this block the shape, the one status cue that is
        // not colour, disappears. Lowercase `canvastext`: NFR-29 reads a capitalised keyword as a
        // literal.
        var block = MediaBlock(Theme(), "@media (forced-colors: active)");

        var marker = RuleIn(block, ".dt-status::before");

        Assert.Contains("forced-color-adjust: none", marker, StringComparison.Ordinal);
        Assert.Contains("background-color: canvastext", marker, StringComparison.Ordinal);

        // The pending ring is a border on a transparent box; painting its background would fill
        // the hollow and make it read as Delivered.
        var ring = RuleIn(block, ".dt-status--pending::before");

        Assert.Contains("background-color: transparent", ring, StringComparison.Ordinal);
        Assert.Contains("border-color: canvastext", ring, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reconnect_dialog_opens_fully_opaque_without_a_fade_under_reduced_motion()
    {
        // The dialog's resting state is `opacity: 0`, and the fade-in's fill is what lifts it.
        // Turning the animation off without restating the opacity leaves a dialog that opens
        // invisible - which under reduced motion is the only way it would ever open.
        var css = WithoutComments(SharedMarkup.ReadComponent("Layout", "ReconnectModal.razor.css"));
        var block = MediaBlock(css, "@media (prefers-reduced-motion: reduce)");

        // Every animated piece - the closed dialog's fade-out, the open dialog's fade-in, the
        // scrim's - and the display/overlay transition, all in the one rule.
        var still = RuleIn(block, "#components-reconnect-modal::backdrop");

        Assert.Contains("animation: none", RuleIn(block, "#components-reconnect-modal[open]"), StringComparison.Ordinal);
        Assert.Contains("animation: none", still, StringComparison.Ordinal);
        Assert.Contains("transition: none", still, StringComparison.Ordinal);

        var opaque = Regex.Match(
            block,
            // Anchored on the previous rule's close, so the match is the rule whose selector list is
            // exactly these two - not the tail of the three-selector rule above it.
            @"\}\s*#components-reconnect-modal\[open\],\s*#components-reconnect-modal::backdrop\s*\{(?<body>[^}]*)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(opaque.Success, "The reduced-motion block does not restate the open dialog's opacity.");
        Assert.Contains("opacity: 1", opaque.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void A_press_settles_the_control_and_reduced_motion_takes_it_away()
    {
        var theme = Theme();

        Assert.Matches(
            @"\.btn:active:not\(:disabled\):not\(\.disabled\),\s*\.dt-nav__link:active\s*\{\s*transform:\s*scale\(0\.97\);",
            theme);

        var reduced = MediaBlocks(theme, "@media (prefers-reduced-motion: reduce)")
            .FirstOrDefault(block => block.Contains(".btn:active", StringComparison.Ordinal));

        Assert.NotNull(reduced);
        Assert.Contains("transform: none", RuleIn(reduced, ".btn:active:not(:disabled):not(.disabled)"), StringComparison.Ordinal);
        Assert.Contains("transition: none", RuleIn(reduced, ".dt-nav__link"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_tapped_button_keeps_its_resting_fill_where_there_is_no_hovering_pointer()
    {
        // The framework's `.btn:hover` fills from `--bs-btn-hover-*`, and a tap leaves a touch
        // screen's button in `:hover` until the next tap lands elsewhere. Where nothing hovers, the
        // hover fill is pointed back at the resting one.
        var button = RuleIn(MediaBlock(Theme(), "@media (hover: none)"), ".btn");

        Assert.Contains("--bs-btn-hover-bg: var(--bs-btn-bg)", button, StringComparison.Ordinal);
        Assert.Contains("--bs-btn-hover-color: var(--bs-btn-color)", button, StringComparison.Ordinal);
        Assert.Contains("--bs-btn-hover-border-color: var(--bs-btn-border-color)", button, StringComparison.Ordinal);
    }

    /// <summary>The declarations of the first rule in <paramref name="css"/> whose selector list names <paramref name="selector"/>.</summary>
    private static string RuleIn(string css, string selector)
    {
        var match = Regex.Match(
            css,
            @"(?:^|[},;{])\s*(?:[^{}]*,\s*)?" + Regex.Escape(selector) + @"\s*(?:,[^{}]*)?\{(?<body>[^}]*)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, $"No rule for '{selector}'.");

        return match.Groups["body"].Value;
    }

    /// <summary>The first block opened by <paramref name="query"/>, braces balanced.</summary>
    private static string MediaBlock(string css, string query)
    {
        var block = MediaBlocks(css, query).FirstOrDefault();

        Assert.True(block is not null, $"No '{query}' block.");

        return block;
    }

    /// <summary>Every block opened by <paramref name="query"/>, braces balanced.</summary>
    private static IEnumerable<string> MediaBlocks(string css, string query)
    {
        for (var start = css.IndexOf(query, StringComparison.Ordinal);
             start >= 0;
             start = css.IndexOf(query, start + query.Length, StringComparison.Ordinal))
        {
            var open = css.IndexOf('{', start);
            var depth = 0;

            for (var index = open; index < css.Length; index++)
            {
                if (css[index] == '{')
                {
                    depth++;
                }
                else if (css[index] == '}' && --depth == 0)
                {
                    yield return css[(open + 1)..index];

                    break;
                }
            }
        }
    }

    /// <summary><c>_theme.scss</c> with its comments removed.</summary>
    private static string Theme() => WithoutComments(File.ReadAllText(Path.Combine(
        RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "Styles", "_theme.scss")));

    private static string WithoutComments(string css) => Regex.Replace(
        Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5)),
        @"(?<!:)//[^\n]*",
        " ",
        RegexOptions.None,
        TimeSpan.FromSeconds(5));
}
