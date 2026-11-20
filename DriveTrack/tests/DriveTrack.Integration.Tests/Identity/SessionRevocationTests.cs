using System.Globalization;
using System.Net;
using DriveTrack.Domain.Accounts;
using DriveTrack.Integration.Tests.Deliveries;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Authentication;

namespace DriveTrack.Integration.Tests.Identity;

/// <summary>
/// The cookie's revalidation, against the real <c>Program.cs</c> pipeline and the cookie the
/// sign-in form actually issues.
/// <para>
/// The check runs at most once per <see cref="SessionValidation.Interval"/>, so each test moves the
/// host's clock past it rather than waiting. The clock is the one the host issues and validates
/// everything with, so the cookie's own lifetime and the bearer tokens the administrator acts with
/// are unaffected by a few minutes' move.
/// </para>
/// </summary>
public class SessionRevocationTests(PostgresFixture postgres)
{
    private static readonly TimeSpan PastTheInterval = SessionValidation.Interval + TimeSpan.FromMinutes(1);

    [Fact]
    public async Task A_deleted_dispatchers_cookie_is_refused_on_the_next_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = await CreateHostAsync(clock, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await AdministrationApi.AdminTokenAsync(client, cancellationToken);
        var dispatcher = await AdministrationApi.CreateDispatcherAsync(client, admin, cancellationToken);
        var cookie = await ProofApi.CookieAsync(
            factory, dispatcher.Email, AdministrationApi.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, (await ProfileAsync(factory, cookie, cancellationToken)).Status);

        using (var deleted = await AdministrationApi.SendAsync(
                   client, HttpMethod.Delete, DispatcherRoute(dispatcher.UserId), admin, body: null, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        clock.Now += PastTheInterval;

        var refused = await ProfileAsync(factory, cookie, cancellationToken);

        AssertSignedOut(refused);
    }

    [Fact]
    public async Task A_cookie_issued_before_an_admin_password_reset_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = await CreateHostAsync(clock, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await AdministrationApi.AdminTokenAsync(client, cancellationToken);
        var dispatcher = await AdministrationApi.CreateDispatcherAsync(client, admin, cancellationToken);
        var before = await ProofApi.CookieAsync(
            factory, dispatcher.Email, AdministrationApi.Password, cancellationToken);

        using (var reset = await AdministrationApi.SendAsync(
                   client,
                   HttpMethod.Patch,
                   DispatcherRoute(dispatcher.UserId),
                   admin,
                   new { password = "New-Passw0rd" },
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        clock.Now += PastTheInterval;

        AssertSignedOut(await ProfileAsync(factory, before, cancellationToken));

        // The account itself is fine: the new password signs in, and that cookie is accepted even
        // once it has been revalidated.
        var after = await ProofApi.CookieAsync(factory, dispatcher.Email, "New-Passw0rd", cancellationToken);

        clock.Now += PastTheInterval;

        Assert.Equal(HttpStatusCode.OK, (await ProfileAsync(factory, after, cancellationToken)).Status);
    }

    [Fact]
    public async Task An_untouched_cookie_keeps_working_and_is_renewed_when_it_is_revalidated()
    {
        // Without this, a check that refused everything would pass both tests above.
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = await CreateHostAsync(clock, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await AdministrationApi.AdminTokenAsync(client, cancellationToken);
        var dispatcher = await AdministrationApi.CreateDispatcherAsync(client, admin, cancellationToken);
        var cookie = await ProofApi.CookieAsync(
            factory, dispatcher.Email, AdministrationApi.Password, cancellationToken);

        clock.Now += PastTheInterval;

        var revalidated = await ProfileAsync(factory, cookie, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, revalidated.Status);

        // Renewed, so the record of this check travels with the cookie and the next requests in
        // the interval are not checked again.
        Assert.NotNull(revalidated.SessionCookie);
        Assert.False(revalidated.SessionCookie.StartsWith("drivetrack.session=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Renaming_and_readdressing_an_account_does_not_end_its_session()
    {
        // Only a password change rolls the stamp. A name or an address is assigned directly on the
        // row, so editing either - the account's own or an administrator's edit - signs nobody out.
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = await CreateHostAsync(clock, cancellationToken);
        using var client = factory.CreateClient();

        var admin = await AdministrationApi.AdminTokenAsync(client, cancellationToken);
        var dispatcher = await AdministrationApi.CreateDispatcherAsync(client, admin, cancellationToken);
        var cookie = await ProofApi.CookieAsync(
            factory, dispatcher.Email, AdministrationApi.Password, cancellationToken);
        var own = await AdministrationApi.SignInAsync(
            client, dispatcher.Email, AdministrationApi.Password, cancellationToken);

        using (var renamed = await AdministrationApi.SendAsync(
                   client,
                   HttpMethod.Patch,
                   DispatcherRoute(dispatcher.UserId),
                   admin,
                   new { firstName = "Богдан" },
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        }

        using (var readdressed = await AdministrationApi.SendAsync(
                   client,
                   HttpMethod.Post,
                   "/api/users/" + dispatcher.UserId.ToString(CultureInfo.InvariantCulture) + "/email",
                   own,
                   new { email = AdministrationApi.UniqueEmail() },
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, readdressed.StatusCode);
        }

        clock.Now += PastTheInterval;

        Assert.Equal(HttpStatusCode.OK, (await ProfileAsync(factory, cookie, cancellationToken)).Status);
    }

    private Task<ApiFactory> CreateHostAsync(FixedTimeProvider clock, CancellationToken cancellationToken) =>
        ApiFactory.CreateAsync(
            postgres.ConnectionString,
            cancellationToken,
            useProbeAuthentication: false,
            clock: clock);

    private static string DispatcherRoute(int userId) =>
        "/api/dispatchers/" + userId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A refused cookie leaves the request anonymous - so a protected page redirects to sign-in -
    /// and is cleared, so the browser stops presenting it.
    /// </summary>
    private static void AssertSignedOut(PageResult result)
    {
        Assert.Equal(HttpStatusCode.Redirect, result.Status);
        Assert.Contains("/sign-in", result.Location, StringComparison.Ordinal);
        Assert.NotNull(result.SessionCookie);
        Assert.StartsWith("drivetrack.session=;", result.SessionCookie, StringComparison.Ordinal);
    }

    /// <summary>Requests a protected page with only the given cookie and reports what came back.</summary>
    private static async Task<PageResult> ProfileAsync(
        ApiFactory factory,
        string cookie,
        CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/profile", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);

        using var response = await client.SendAsync(request, cancellationToken);

        var session = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(value => value.StartsWith("drivetrack.session=", StringComparison.Ordinal))
            : null;

        return new PageResult(response.StatusCode, response.Headers.Location?.OriginalString, session);
    }

    private sealed record PageResult(HttpStatusCode Status, string? Location, string? SessionCookie);
}
