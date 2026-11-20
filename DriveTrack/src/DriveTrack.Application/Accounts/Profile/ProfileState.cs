namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// A profile after the payload has been merged onto what is stored. This — not the command —
/// is what the validator sees, so a rule holds over the row as it will exist.
/// </summary>
/// <param name="FirstName">Given name as it will be.</param>
/// <param name="LastName">Family name as it will be.</param>
/// <param name="PhoneNumber">Contact number as it will be, or null when there is none.</param>
/// <param name="IsClient">
/// Whether this account has a client row behind it. Carried on the merged state rather than read as
/// a role inside the service, so "only a client has a phone number" is a property of the row the
/// validator is judging instead of a branch somewhere else that the validator cannot see.
/// </param>
public sealed record ProfileState(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    bool IsClient);
