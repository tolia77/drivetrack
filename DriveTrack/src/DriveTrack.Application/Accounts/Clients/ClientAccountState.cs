namespace DriveTrack.Application.Accounts.Clients;

/// <summary>
/// A client account after the payload has been merged onto what is stored. This — not the
/// command — is what the validator sees, so a rule holds over the row as it will exist rather than
/// over the handful of fields this particular request happened to mention.
/// </summary>
/// <param name="FirstName">Given name as it will be.</param>
/// <param name="LastName">Family name as it will be.</param>
/// <param name="PhoneNumber">Contact number as it will be.</param>
/// <param name="Password">
/// The new password, or null for unchanged. There is no current plaintext to merge against — only
/// a hash is stored — so absent stays null here and means "not being set".
/// </param>
/// <param name="Email">The address as it will be. Unlike the password, this one is stored and merges.</param>
public sealed record ClientAccountState(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Password,
    string? Email);
