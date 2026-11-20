using DriveTrack.Application.Accounts;
using DriveTrack.Application.Accounts.Dispatchers;
using DriveTrack.Application.Common;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// The identity fields of an account that does not exist yet. The password travels separately and
/// only ever reaches storage through Identity's <c>IPasswordHasher</c>, which is why it is
/// not a property of this record.
/// </summary>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account will sign in with; also its user name.</param>
public sealed record NewUserAccount(string FirstName, string LastName, string Email);
