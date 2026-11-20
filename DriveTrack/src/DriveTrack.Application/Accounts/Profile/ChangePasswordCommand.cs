using DriveTrack.Application.Common;

namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// What a signed-in user sends to replace their own password.
/// <para>
/// Not a merge, and not an <see cref="Optional{T}"/> anywhere: there is no stored plaintext to fall
/// back on, and every one of the three fields has to be present for the operation to mean anything.
/// The current password is what makes this different from an administrator's reset — a borrowed
/// session cannot change the password without knowing the old one.
/// </para>
/// </summary>
/// <param name="CurrentPassword">The password the account signs in with today.</param>
/// <param name="NewPassword">The password it will sign in with; hashed by Identity.</param>
/// <param name="NewPasswordConfirmation">The new password again, to catch a typo before it is stored.</param>
public sealed record ChangePasswordCommand(
    string? CurrentPassword,
    string? NewPassword,
    string? NewPasswordConfirmation);
