using DriveTrack.Application.Accounts;
using DriveTrack.Application.Accounts.Dispatchers;
using DriveTrack.Application.Common;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// An account as this layer knows it: the identity fields plus the single role, and the subtype row
/// id when the role has one. Application never sees <c>ApplicationUser</c> or any other Identity
/// type — the whole of ASP.NET Core Identity is behind this record and the port below.
/// </summary>
/// <param name="Id">The user row's id.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account signs in with.</param>
/// <param name="Role">The single role.</param>
/// <param name="DriverId">The driver row id, or null when the user is not a driver.</param>
/// <param name="ClientId">The client row id, or null when the user is not a client.</param>
public sealed record UserAccount(
    UserId Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    DriverId? DriverId,
    ClientId? ClientId)
{
    /// <summary>The trimmed <c>"First Last"</c> name the account is shown by (<see cref="PersonNames"/>).</summary>
    public string DisplayName => PersonNames.Display(FirstName, LastName);
}
