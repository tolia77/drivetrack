using System.Security.Claims;
using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Authorization;

namespace DriveTrack.Web.Authentication;

/// <summary>
/// The one place that turns a session into the claims a scheme issues.
/// <para>
/// The bearer token is signed in Infrastructure and the cookie is written here, and the two must
/// carry the same claim names and the same values: a person signed in through the Blazor form and
/// the same person signed in through <c>/api/auth/sign-in</c> have to come back as the same
/// <see cref="ICurrentUser"/>. The names and the values both come from <see cref="SessionClaims"/>,
/// in the layer both sides reference; this file is the cookie half of that agreement.
/// </para>
/// </summary>
internal static class ClaimsFactory
{
    /// <summary>
    /// The identity's authentication type. It has to be non-empty for
    /// <c>ClaimsIdentity.IsAuthenticated</c> to be true, which is what <c>&lt;AuthorizeView&gt;</c>
    /// and <c>[Authorize]</c> read.
    /// </summary>
    public const string AuthenticationType = "DriveTrack";

    /// <summary>
    /// The account's security stamp at sign-in. Cookie-only: the bearer token does not carry it,
    /// which is why the name lives here rather than in <c>DriveTrackClaimTypes</c>.
    /// <see cref="SessionValidation"/> compares it with the stored stamp to end a session whose
    /// password has been replaced or whose account has gone.
    /// </summary>
    public const string SecurityStampClaimType = "drivetrack/security-stamp";

    /// <summary>Builds the principal the cookie scheme signs in.</summary>
    public static ClaimsPrincipal Build(AuthenticatedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // The same list the bearer token is signed with, so the two schemes cannot drift apart.
        var claims = SessionClaims.For(session).Select(claim => new Claim(claim.Type, claim.Value)).ToList();

        if (session.SecurityStamp is { } stamp)
        {
            claims.Add(new Claim(SecurityStampClaimType, stamp));
        }

        // The name and role claim types are named explicitly so User.Identity.Name and the role
        // checks read the claims this file writes rather than the framework's defaults, which are
        // the long WS-Federation URIs nothing here issues.
        var identity = new ClaimsIdentity(
            claims,
            AuthenticationType,
            DriveTrackClaimTypes.Name,
            DriveTrackClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
