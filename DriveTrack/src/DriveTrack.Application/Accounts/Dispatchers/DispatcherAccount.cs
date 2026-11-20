
using DriveTrack.Domain.Accounts;

namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// A dispatcher account as an administrator reads it. There is no dispatcher subtype row,
/// so this is the user and nothing else.
/// </summary>
/// <param name="UserId">The account.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account signs in with; changed through <c>IUserService.ChangeEmailAsync</c>.</param>
public sealed record DispatcherAccount(UserId UserId, string FirstName, string LastName, string Email);
