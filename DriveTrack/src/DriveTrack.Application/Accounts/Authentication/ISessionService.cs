using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Accounts.Authentication;

/// <summary>
/// Whether a session that was valid when it was issued still belongs to its account.
/// <para>
/// The adapters ask this of a session they already hold - a cookie being revalidated, a live
/// circuit, a hub connection - with that session's principal as the caller. So the one guard call
/// is <c>RequireSelf</c>: the user asking about their own session, which is why this capability
/// needs no entry in <see cref="Authorization.PublicEntryPoints"/>.
/// </para>
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// True when the account <paramref name="userId"/> still exists and, when
    /// <paramref name="securityStamp"/> is given, its Identity security stamp has not rolled since
    /// the session began - a replaced password rolls it. The caller must be that user, or hold
    /// <see cref="UserRole.Admin"/>.
    /// <para>
    /// A null stamp checks existence only. It is for a credential that carries no stamp, which is a
    /// bearer token; a cookie always carries one, and the adapter refuses a cookie without it
    /// rather than asking here.
    /// </para>
    /// </summary>
    Task<bool> IsCurrentAsync(UserId userId, string? securityStamp, CancellationToken cancellationToken);
}
