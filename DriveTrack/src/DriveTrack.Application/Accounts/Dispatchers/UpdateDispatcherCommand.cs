using DriveTrack.Application.Common;

namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// A partial edit of a dispatcher. No email and no role: the first changes through
/// <c>IUserService.ChangeEmailAsync</c>, the second is fixed for the life of an account.
/// </summary>
/// <param name="FirstName">Given name, or absent.</param>
/// <param name="LastName">Family name, or absent.</param>
/// <param name="Password">A new password, or absent for unchanged.</param>
public sealed record UpdateDispatcherCommand(
    Optional<string?> FirstName,
    Optional<string?> LastName,
    Optional<string?> Password);
