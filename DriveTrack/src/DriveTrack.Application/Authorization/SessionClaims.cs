using System.Globalization;
using DriveTrack.Application.Abstractions.Persistence;
using DriveTrack.Application.Accounts;
using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Authorization;

/// <summary>
/// The one list of identity claims both schemes issue for a signed-in person.
/// <para>
/// The bearer token is signed in Infrastructure and the cookie is written by the Blazor adapter, and
/// the two must carry the same claims with the same values: a person signed in through the form and
/// the same person signed in through <c>/api/auth/sign-in</c> have to come back as the same
/// <see cref="ICurrentUser"/>. <see cref="DriveTrackClaimTypes"/> makes the names agree; this makes
/// the values agree, by building them in the layer both sides reference.
/// </para>
/// <para>
/// Pairs of strings rather than <c>System.Security.Claims.Claim</c>, for the reason
/// <see cref="DriveTrackClaimTypes"/> gives: the caller's adapter shape stays out of this layer,
/// and each scheme turns these into its own claims. Anything only one scheme needs is
/// that scheme's to add on top.
/// </para>
/// </summary>
public static class SessionClaims
{
    /// <summary>The claims for an account, as the bearer token carries them.</summary>
    public static IReadOnlyList<SessionClaim> For(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return Build(
            account.Id,
            account.Role,
            account.DisplayName,
            account.Email,
            account.DriverId,
            account.ClientId);
    }

    /// <summary>The claims for a session, as the cookie carries them.</summary>
    public static IReadOnlyList<SessionClaim> For(AuthenticatedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return Build(
            session.UserId,
            session.Role,
            PersonNames.Display(session.FirstName, session.LastName),
            session.Email,
            session.DriverId,
            session.ClientId);
    }

    private static List<SessionClaim> Build(
        UserId userId,
        UserRole role,
        string displayName,
        string email,
        DriverId? driverId,
        ClientId? clientId)
    {
        var claims = new List<SessionClaim>
        {
            new(DriveTrackClaimTypes.UserId, userId.Value.ToString(CultureInfo.InvariantCulture)),
            new(DriveTrackClaimTypes.Role, role.ToString()),
            new(DriveTrackClaimTypes.Name, displayName),
            new(DriveTrackClaimTypes.Email, email),
        };

        // The subtype id travels only when there is one. An empty claim would be a value every
        // reader has to remember to treat as absent, which is the kind of thing readers forget.
        if (driverId is { } driver)
        {
            claims.Add(new(DriveTrackClaimTypes.DriverId, driver.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (clientId is { } client)
        {
            claims.Add(new(DriveTrackClaimTypes.ClientId, client.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return claims;
    }
}
