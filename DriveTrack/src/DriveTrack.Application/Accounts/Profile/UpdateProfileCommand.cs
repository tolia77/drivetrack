using DriveTrack.Application.Common;

namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// A partial edit of a user's own profile. Every field is an <see cref="Optional{T}"/>:
/// absent means unchanged, present-null means the caller sent the field and sent nothing in it.
/// <para>
/// There is no email and no password here. Both are separate operations with rules of their own —
/// an address can collide with another account's and a password needs the current one first — and
/// folding either into a general "save my profile" would hide that behind a field nobody had to
/// think about.
/// </para>
/// </summary>
/// <param name="FirstName">Given name, or absent.</param>
/// <param name="LastName">Family name, or absent.</param>
/// <param name="PhoneNumber">
/// Contact number in E.164 form, or absent. Only a client has a row to store one in, so a
/// caller of any other role who sends it is refused rather than quietly ignored.
/// </param>
public sealed record UpdateProfileCommand(
    Optional<string?> FirstName,
    Optional<string?> LastName,
    Optional<string?> PhoneNumber);
