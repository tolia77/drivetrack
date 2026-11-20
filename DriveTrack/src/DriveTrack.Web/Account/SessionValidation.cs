using System.Globalization;
using System.Security.Claims;
using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Authorization;
using DriveTrack.Domain.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DriveTrack.Web.Account;

/// <summary>
/// Ends a session whose account has been deleted or whose password has been replaced since it was
/// issued.
/// <para>
/// The cookie is seven days and sliding, so without this a deleted dispatcher's browser, or a
/// session opened before an administrator reset the password, kept working for as long as it kept
/// being used. The cookie carries the account's security stamp (<see cref="ClaimsFactory"/>);
/// Identity rolls the stamp when a password is set, and a deleted account has none at all, so
/// comparing it with the stored one answers both cases. Changing a name or an email address does
/// not roll it - those are assigned directly on the row - so neither signs anybody out.
/// </para>
/// <para>
/// Throttled to once per <see cref="Interval"/> per cookie rather than run on every request: the
/// cookie handler authenticates every request that carries the cookie, static assets included, and
/// a page load is several of those. The time of the last check travels in the cookie's own
/// properties, so a revoked session is refused at most one interval after the change.
/// </para>
/// </summary>
internal static class SessionValidation
{
    /// <summary>
    /// How long a validated cookie, circuit or hub connection is trusted before it is checked
    /// against the account again.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>The authentication property holding when the cookie was last validated.</summary>
    private const string ValidatedAtKey = ".drivetrack.validated-at";

    /// <summary>
    /// Records sign-in as the first validation: the credentials were checked a moment ago, so the
    /// first requests after it need not ask again.
    /// </summary>
    public static Task OnSigningInAsync(CookieSigningInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Record(context.Properties, Clock(context.HttpContext).GetUtcNow());

        return Task.CompletedTask;
    }

    /// <summary>
    /// The cookie scheme's <c>OnValidatePrincipal</c>: once an interval has passed since the last
    /// check, rejects and clears a cookie that is no longer its account's, and otherwise records
    /// the check and renews the cookie so the record travels with it.
    /// </summary>
    public static async Task OnValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal is not { } principal)
        {
            return;
        }

        var now = Clock(context.HttpContext).GetUtcNow();

        // A record from the future - a clock moved back - is treated as due rather than trusted.
        if (LastValidated(context.Properties) is { } last && last <= now && now - last < Interval)
        {
            return;
        }

        var http = context.HttpContext;
        var sessions = http.RequestServices.GetRequiredService<ISessionService>();

        // The service's guard reads the caller from HttpContext.User, which the authentication
        // middleware only sets once this event has answered. So the principal under judgement is
        // presented as the caller for the one call, and the previous value is always put back: a
        // rejected principal must not be left behind as the request's user.
        var previous = http.User;
        bool current;

        try
        {
            http.User = principal;
            current = await IsCurrentAsync(principal, sessions, http.RequestAborted);
        }
        finally
        {
            http.User = previous;
        }

        if (!current)
        {
            context.RejectPrincipal();
            await http.SignOutAsync(context.Scheme.Name);

            return;
        }

        Record(context.Properties, now);
        context.ShouldRenew = true;
    }

    /// <summary>
    /// Whether <paramref name="principal"/> is still its account's session. The caller must have
    /// made <paramref name="principal"/> the current user of the scope <paramref name="sessions"/>
    /// was resolved from, because the service's guard reads it there.
    /// <para>
    /// A principal the cookie scheme issued must carry the stamp; one without it predates this check
    /// and is refused, so its user signs in once more. A bearer principal carries none and is
    /// checked for the account's existence only - its token's own expiry bounds the rest.
    /// </para>
    /// </summary>
    public static async Task<bool> IsCurrentAsync(
        ClaimsPrincipal principal,
        ISessionService sessions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(sessions);

        if (!int.TryParse(
                principal.FindFirstValue(DriveTrackClaimTypes.UserId),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var userId))
        {
            return false;
        }

        var stamp = principal.FindFirstValue(ClaimsFactory.SecurityStampClaimType);
        var issuedAsCookie = string.Equals(
            principal.Identity?.AuthenticationType,
            ClaimsFactory.AuthenticationType,
            StringComparison.Ordinal);

        if (issuedAsCookie && stamp is null)
        {
            return false;
        }

        return await sessions.IsCurrentAsync(new UserId(userId), stamp, cancellationToken);
    }

    /// <summary>The injected clock, never the machine's.</summary>
    private static TimeProvider Clock(HttpContext context) =>
        context.RequestServices.GetRequiredService<TimeProvider>();

    private static void Record(AuthenticationProperties properties, DateTimeOffset at) =>
        properties.Items[ValidatedAtKey] = at.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? LastValidated(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(ValidatedAtKey, out var value)
        && DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var at)
            ? at
            : null;
}
