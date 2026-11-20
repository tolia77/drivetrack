namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// What a signed-in user sends to replace the address their account signs in with.
/// <para>
/// One field, and its own operation rather than a field on <see cref="UpdateProfileCommand"/>: this
/// is the only edit on the profile that can collide with another account, and the refusal it can
/// raise is a 409 rather than a 422.
/// </para>
/// </summary>
/// <param name="Email">The address the account will sign in with.</param>
public sealed record ChangeEmailCommand(string? Email);
