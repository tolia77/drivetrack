using System.Net;
using System.Text.RegularExpressions;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;

namespace DriveTrack.Integration.Tests.Design;

/// <summary>
/// The Development-only asset headers in <c>Program.cs</c>: request validators dropped, responses
/// marked <c>no-store</c>. Without them a browser revalidates a fingerprinted asset against a
/// build-time ETag that <c>dotnet watch</c> never changes, gets a 304, and keeps serving the stale
/// file - an edit to a stylesheet appears to do nothing.
/// </summary>
/// <remarks>
/// <c>ApiFactory</c>'s host runs in Development, which is the environment compose.dev.yaml pins.
/// </remarks>
public class DevelopmentAssetCacheTests(PostgresFixture postgres)
{
    private static readonly Regex StylesheetHref = new(
        @"href\s*=\s*[""'](?<href>[^""']*css/app[^""']*\.css)[""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public async Task A_conditional_request_for_the_theme_is_answered_in_full_and_not_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = await ApiFactory.CreateAsync(postgres.ConnectionString, cancellationToken);
        using var client = factory.CreateClient();

        using var shellResponse = await client.GetAsync(
            new Uri("/does-not-exist", UriKind.Relative),
            cancellationToken);

        var href = StylesheetHref.Match(await shellResponse.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(href.Success, "The rendered shell links no css/app*.css.");

        var theme = new Uri(href.Groups["href"].Value.TrimStart('/'), UriKind.Relative);

        using var first = await client.GetAsync(theme, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The validator a browser would send back. Without one this test could not tell a stripped
        // If-None-Match from one that simply matched nothing.
        var etag = first.Headers.ETag;

        Assert.NotNull(etag);

        using var revalidation = new HttpRequestMessage(HttpMethod.Get, theme);
        revalidation.Headers.IfNoneMatch.Add(etag);
        revalidation.Headers.IfModifiedSince = first.Content.Headers.LastModified ?? DateTimeOffset.UtcNow;

        using var second = await client.SendAsync(revalidation, cancellationToken);

        // 200 with the bytes, not 304: the current file, whatever the build-time ETag says.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("--dt-action-create", await second.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);

        Assert.NotNull(second.Headers.CacheControl);
        Assert.True(
            second.Headers.CacheControl.NoStore,
            $"Cache-Control was '{second.Headers.CacheControl}', not no-store.");
    }
}
