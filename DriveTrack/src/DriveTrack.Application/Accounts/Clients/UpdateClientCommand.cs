using DriveTrack.Application.Common;

namespace DriveTrack.Application.Accounts.Clients;

/// <summary>
/// A partial edit of a client. Every field is an <see cref="Optional{T}"/>: absent means
/// unchanged, present-null means the caller sent nothing in a field they did send.
/// <para>
/// The password is why the distinction has to be a type rather than a convention. A blank password
/// box on an edit form means "leave it alone", which an ordinary nullable string cannot say without
/// also being the way to say "clear it" — and clearing a password is not an operation this system
/// has.
/// </para>
/// </summary>
/// <param name="FirstName">Given name, or absent.</param>
/// <param name="LastName">Family name, or absent.</param>
/// <param name="PhoneNumber">Contact number in E.164 form, or absent.</param>
/// <param name="Password">A new password, or absent for unchanged.</param>
/// <param name="Email">
/// The address the account signs in with, or absent for unchanged. It rides the edit the
/// administrator is already performing rather than being an operation of its own, so a taken address
/// rolls the name and the phone number back with it.
/// </param>
public sealed record UpdateClientCommand(
    Optional<string?> FirstName,
    Optional<string?> LastName,
    Optional<string?> PhoneNumber,
    Optional<string?> Password,
    Optional<string?> Email);
