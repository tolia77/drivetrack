namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// What an administrator sends to open a dispatcher account. Not partial: a new account has
/// no stored state to merge onto, so every field is required and plainly nullable.
/// </summary>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account will sign in with. The only chance to set it.</param>
/// <param name="Password">The chosen password; hashed by Identity and never stored as sent.</param>
public sealed record CreateDispatcherCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password);
