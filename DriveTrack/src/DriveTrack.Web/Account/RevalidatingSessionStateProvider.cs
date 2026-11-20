using DriveTrack.Application.Accounts.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace DriveTrack.Web.Account;

/// <summary>
/// The circuit half of <see cref="SessionValidation"/>.
/// <para>
/// An interactive screen runs on a circuit that read its principal once, when it opened, and sends
/// no further request for the cookie handler to judge - so a dispatcher deleted while their tab is
/// open would keep working until they reloaded it. This re-asks the same question every
/// <see cref="SessionValidation.Interval"/>, and a session that is no longer its account's becomes
/// anonymous, which the shell already answers by sending the user to sign in.
/// </para>
/// </summary>
internal sealed class RevalidatingSessionStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    /// <inheritdoc />
    protected override TimeSpan RevalidationInterval => SessionValidation.Interval;

    /// <inheritdoc />
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        var principal = authenticationState.User;

        // Nobody signed in has no session to end.
        if (principal.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        // A scope of its own, because the circuit's scope is busy rendering. It has no HttpContext
        // and no circuit state, so the principal is handed over through HubCaller - the holder
        // CurrentUser reads for a caller that has neither - and the service's guard finds it there.
        await using var scope = scopeFactory.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<HubCaller>().Principal = principal;

        return await SessionValidation.IsCurrentAsync(
            principal,
            scope.ServiceProvider.GetRequiredService<ISessionService>(),
            cancellationToken);
    }
}
