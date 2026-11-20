namespace DriveTrack.Application.Accounts.Dispatchers;

/// <inheritdoc cref="ClientAccountState" />
/// <param name="FirstName">Given name as it will be.</param>
/// <param name="LastName">Family name as it will be.</param>
/// <param name="Password">The new password, or null for unchanged.</param>
public sealed record DispatcherAccountState(string? FirstName, string? LastName, string? Password);
