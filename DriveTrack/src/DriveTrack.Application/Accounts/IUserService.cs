using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Accounts.Profile;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Accounts;

/// <summary>
/// The account capability: opening an account, entering it,
/// and the four things a user does to their own — read it, edit it, change its password, change its
/// address.
/// <para>
/// Two members are in <see cref="Authorization.PublicEntryPoints"/> because an anonymous caller
/// performs them; every other one calls <see cref="Authorization.IAccessGuard"/>. There is no third
/// option — <c>GuardCoverageTests</c> fails the build for a public method that is neither.
/// </para>
/// <para>
/// All four self-service members ask <c>RequireSelf</c>, and that single question is the whole of
/// their authorization. The admin override puts "an admin satisfies every check" inside the guard,
/// once, so <c>RequireSelf</c> already reads as "the user themselves, or an admin" — which is
/// exactly the email change's two halves. A role branch inside this capability to narrow the admin case would be a second place
/// authorization lived.
/// </para>
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Opens a client account and returns the session it starts. Anonymous by design.
    /// </summary>
    Task<AuthenticatedSession> RegisterClientAsync(
        RegisterClientCommand command,
        CancellationToken cancellationToken);

    /// <summary>Exchanges credentials for a session. Anonymous by design.</summary>
    Task<AuthenticatedSession> SignInAsync(SignInCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a user's profile. The caller must be that user, or hold
    /// <see cref="UserRole.Admin"/>.
    /// </summary>
    Task<UserProfile> GetProfileAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Edits a user's own name, and their phone number when they are a client. The caller
    /// must be that user, or hold <see cref="UserRole.Admin"/>.
    /// <para>
    /// Partial: a field the command leaves absent is left alone. A phone number sent by an
    /// account with no client row behind it is refused rather than dropped.
    /// </para>
    /// </summary>
    /// <returns>The profile as it stands after the edit.</returns>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    /// <exception cref="Common.ValidationException">The merged profile breaks a rule.</exception>
    Task<UserProfile> UpdateProfileAsync(
        UserId userId,
        UpdateProfileCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a user's own password, having checked the current one. The caller must be
    /// that user, or hold <see cref="UserRole.Admin"/>.
    /// <para>
    /// Nothing is returned: a password has no readable form, so there is no state to hand
    /// back. The change rolls the account's security stamp, so every cookie session issued before
    /// it - the caller's own included - is refused at its next revalidation and has to sign in with
    /// the new password. A bearer token carries no stamp and runs to its own expiry.
    /// </para>
    /// </summary>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    /// <exception cref="Common.ValidationException">
    /// The command breaks a rule, the current password does not match
    /// (<c>AUTH_CURRENT_PASSWORD_INCORRECT</c>), or Identity's own policy refused the new one.
    /// </exception>
    Task ChangePasswordAsync(
        UserId userId,
        ChangePasswordCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the address a user's account signs in with. The caller must be that user, or
    /// hold <see cref="UserRole.Admin"/> — which is what gives the change its administrator half without
    /// a second guard member.
    /// <para>
    /// Asking for the address the account already holds, in any casing, is not a conflict and is not
    /// a write.
    /// </para>
    /// </summary>
    /// <returns>The profile as it stands after the change.</returns>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    /// <exception cref="Common.ValidationException">The address is absent or malformed.</exception>
    /// <exception cref="Common.ConflictException">Another account already holds that address.</exception>
    Task<UserProfile> ChangeEmailAsync(
        UserId userId,
        ChangeEmailCommand command,
        CancellationToken cancellationToken);
}
