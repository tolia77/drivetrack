
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;

namespace DriveTrack.Application.Accounts.Clients;

/// <summary>
/// A client account as an administrator or dispatcher reads it. A DTO, so neither
/// <c>Client</c> nor <c>ApplicationUser</c> crosses an adapter boundary.
/// </summary>
/// <param name="UserId">The account this client is. Every operation addresses a client by this.</param>
/// <param name="ClientId">The client row's own id, distinct from the user's.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account signs in with, and which an administrator may change.</param>
/// <param name="PhoneNumber">Contact number in E.164 form.</param>
public sealed record ClientAccount(
    UserId UserId,
    ClientId ClientId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber);
