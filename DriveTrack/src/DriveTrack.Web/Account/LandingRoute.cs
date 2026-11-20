
using DriveTrack.Domain.Accounts;

namespace DriveTrack.Web.Account;

/// <summary>
/// Where a role lands after signing in, decided in one place.
/// <para>
/// Admin and Dispatcher land on <c>/dashboard</c>; Driver and Client land on <c>/profile</c> until
/// they have a dashboard of their own. Writing the switch out in full is what makes that a one-line
/// change here instead of a search for redirect literals.
/// </para>
/// </summary>
internal static class LandingRoute
{
    /// <summary>The route a signed-in user of that role is sent to.</summary>
    public static string For(UserRole role) => role switch
    {
        // The two roles that run dispatch land on the day at a glance; a driver and a client keep
        // their profile until they have a dashboard of their own.
        UserRole.Admin => "/dashboard",
        UserRole.Dispatcher => "/dashboard",
        UserRole.Driver => "/profile",
        UserRole.Client => "/profile",

        // Total, like the status map: a fifth role added without a landing screen fails loudly here
        // rather than silently sending someone to the home page.
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "No landing route is mapped for this role."),
    };
}
