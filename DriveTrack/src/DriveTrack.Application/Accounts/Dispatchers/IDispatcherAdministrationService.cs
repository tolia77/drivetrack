using DriveTrack.Application.Common.Paging;
using DriveTrack.Domain.Accounts;

namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// Dispatcher accounts. The original's <c>/dispatchers</c> endpoints, and the one way in the
/// system to create a dispatcher besides the startup seeder.
/// <para>
/// Every method is <c>RequireRole(Admin)</c>: a dispatcher administers deliveries, not colleagues.
/// </para>
/// <para>
/// Every method also addresses a <em>dispatcher</em>. A user id belonging to an admin, a driver or
/// a client answers <c>COMMON_NOT_FOUND</c>, so this surface can never rename or delete an account
/// it was not written to manage — and cannot be used to discover which ids exist.
/// </para>
/// </summary>
public interface IDispatcherAdministrationService
{
    /// <summary>Opens a dispatcher account and returns it.</summary>
    /// <exception cref="Common.ConflictException">Another account already holds that email.</exception>
    Task<DispatcherAccount> CreateAsync(
        CreateDispatcherCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of the dispatcher roster, sorted over every dispatcher, with the size of the roster.
    /// Administrators only.
    /// </summary>
    /// <exception cref="Common.ForbiddenException">The caller does not run the system.</exception>
    /// <exception cref="Common.Errors.ValidationException">
    /// The paging parameters are out of range, or the sort names no column.
    /// </exception>
    Task<Common.Paging.Paged<DispatcherAccount>> ListAsync(
        ListDispatchersQuery query,
        CancellationToken cancellationToken);

    /// <summary>One dispatcher.</summary>
    /// <exception cref="Common.NotFoundException">
    /// No account has that id, or the account is not a dispatcher.
    /// </exception>
    Task<DispatcherAccount> GetAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Edits a dispatcher's name and password. A command that leaves the password absent
    /// leaves the stored password exactly as it is.
    /// </summary>
    Task<DispatcherAccount> UpdateAsync(
        UserId userId,
        UpdateDispatcherCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a dispatcher's account. The timeline entries, chat messages and proofs they
    /// touched survive with a null actor reference rather than being erased or made undeletable.
    /// </summary>
    Task DeleteAsync(UserId userId, CancellationToken cancellationToken);
}
