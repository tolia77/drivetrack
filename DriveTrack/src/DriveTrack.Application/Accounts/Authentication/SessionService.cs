using DriveTrack.Application.Abstractions.Persistence;
using DriveTrack.Application.Authorization;
using DriveTrack.Domain.Accounts;

namespace DriveTrack.Application.Accounts.Authentication;

/// <summary>
/// The fixed operation pipeline, read-only: load → guard → answer. Nothing is written, so the scope is disposed
/// without a commit.
/// </summary>
public sealed class SessionService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IAccessGuard accessGuard) : ISessionService
{
    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(
        UserId userId,
        string? securityStamp,
        CancellationToken cancellationToken)
    {
        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var stored = await unitOfWork.Users.FindSecurityStampAsync(userId, cancellationToken);

        // Load, then guard. The guard reads only the caller's claims, so it passes for the session's
        // own user even when the row behind it is gone - which is the case this exists to answer.
        accessGuard.RequireSelf(userId);

        if (stored is null)
        {
            return false;
        }

        return securityStamp is null || string.Equals(stored, securityStamp, StringComparison.Ordinal);
    }
}
