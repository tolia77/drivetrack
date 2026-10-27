using System.Text.RegularExpressions;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Components.Illustrations;
using Microsoft.AspNetCore.Components;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The pictures under <c>Components/Illustrations/</c> are decoration, and that is the condition of
/// their exemption from NFR-29 (see <c>DesignTokenTests.ProjectFiles</c>): each is hidden from
/// assistive technology as a whole and holds nothing a keyboard or a pointer can act on. A picture
/// of a button that took focus would be a focus stop inside an <c>aria-hidden</c> subtree - one a
/// screen reader lands on and cannot name.
/// <para>
/// Rendered rather than read off the source, so what a child component draws inside a picture - a
/// status label, a rating - is held to the same rule.
/// </para>
/// </summary>
public class IllustrationTests
{
    /// <summary>Elements that can be focused or acted on, and the attribute that makes anything focusable.</summary>
    private static readonly Regex Interactive = new(
        @"<(?:a|button|input|select|textarea|details|summary|iframe)\b|\btabindex=|\bhref=|\bcontenteditable",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>The opening tag of the first element a component renders.</summary>
    private static readonly Regex RootTag = new(
        @"^\s*<[a-z]+\b[^>]*>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    public static TheoryData<string> Illustrations() =>
    [
        .. IllustrationTypes().Select(type => type.Name),
    ];

    [Fact]
    public void Every_component_in_the_folder_is_checked_here()
    {
        // The theory below is driven by the assembly, the folder by the file system; this ties the
        // two together, so a picture added under Illustrations/ cannot be left out of the rule.
        var files = Directory.GetFiles(
                Path.Combine(SharedMarkup.ComponentsDirectory, "Illustrations"),
                "*.razor")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        Assert.Equal(
            files,
            IllustrationTypes().Select(type => type.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [MemberData(nameof(Illustrations))]
    public async Task An_illustration_is_hidden_from_assistive_technology_and_holds_nothing_to_act_on(
        string name)
    {
        var type = IllustrationTypes().Single(candidate => candidate.Name == name);
        var html = await Render(type);

        var root = RootTag.Match(html);

        Assert.True(root.Success, $"{name} renders no element:{Environment.NewLine}{html}");
        Assert.Contains("aria-hidden=\"true\"", root.Value, StringComparison.Ordinal);

        var interactive = Interactive.Matches(html).Select(match => match.Value).ToArray();

        Assert.True(
            interactive.Length == 0,
            $"{name} draws something that can be acted on ({string.Join(", ", interactive)}):"
                + $"{Environment.NewLine}{html}");
    }

    [Theory]
    [InlineData(typeof(LandingHeroScene), "LandingPictureDeliveryTime")]
    [InlineData(typeof(RequestIllustration), "LandingPictureFrom")]
    [InlineData(typeof(UpdatesIllustration), "LandingPictureMailOnItsWay")]
    [InlineData(typeof(PhoneDeliveries), "MyDeliveriesTitle")]
    public async Task The_words_an_illustration_draws_follow_the_reader_s_language(Type type, string key)
    {
        // The pictures' words are catalogue entries, so the language switch reaches them; the
        // addresses beside them are literals and stay Ukrainian in both.
        var ukrainian = await Render(type);
        var english = await Render(type, new System.Globalization.CultureInfo("en-US"));

        Assert.Contains($">{Catalogue("UiText.resx", key)}", ukrainian, StringComparison.Ordinal);
        Assert.Contains($">{Catalogue("UiText.en.resx", key)}", english, StringComparison.Ordinal);
        Assert.DoesNotContain($">{Catalogue("UiText.resx", key)}", english, StringComparison.Ordinal);
    }

    /// <summary>A value from one of the UiText catalogues.</summary>
    private static string Catalogue(string fileName, string key)
    {
        var path = Path.Combine(RepositoryLayout.ProjectDirectory("DriveTrack.Web"), "Resources", fileName);
        var value = System.Xml.Linq.XDocument.Load(path).Root!
            .Elements("data")
            .SingleOrDefault(element => element.Attribute("name")?.Value == key)
            ?.Element("value")?.Value;

        Assert.False(string.IsNullOrEmpty(value), $"{fileName} has no {key}.");

        return value!;
    }

    private static IEnumerable<Type> IllustrationTypes() =>
        typeof(LandingHeroScene).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(LandingHeroScene).Namespace)
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract);

    private static Task<string> Render(Type type, System.Globalization.CultureInfo? culture = null)
    {
        var method = typeof(ComponentRenderer)
            .GetMethods()
            .Single(candidate => candidate.Name == nameof(ComponentRenderer.RenderAsync)
                && candidate.IsGenericMethodDefinition
                && candidate.GetParameters().Length == 3)
            .MakeGenericMethod(type);

        return (Task<string>)method.Invoke(null, [null, null, culture])!;
    }
}
