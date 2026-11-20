using DriveTrack.Application.Accounts;
using DriveTrack.Application.Accounts.Dispatchers;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// The Identity port. It joins the repository roster on <see cref="IUnitOfWork"/>, so
/// a user, its role row and its subtype row are staged against one context inside one transaction
/// and land together or not at all.
/// <para>
/// The repository rules hold here as they do for every other repository: materialized results, no
/// <c>IQueryable</c>, a cancellation token on every I/O method, and nothing Identity-shaped
/// crossing back into Application.
/// </para>
/// </summary>
public interface IUserAccountRepository
{
    /// <summary>Loads the account with that email, or null when there is none.</summary>
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Loads the account with that id, or null when there is none.</summary>
    Task<UserAccount?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the account behind a client row, or null when there is none.
    /// <para>
    /// A client's address lives on the Identity account and nowhere else — neither <c>Client</c>
    /// nor <c>Delivery</c> carries one — so notifying the client of a delivery means starting from
    /// its <see cref="ClientId"/> and arriving here. Asked as its own question rather than by
    /// scanning <see cref="ListByRoleAsync"/> for a match: a background job that wants one address
    /// must not read the whole client roster to find it.
    /// </para>
    /// </summary>
    Task<UserAccount?> FindByClientIdAsync(ClientId clientId, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts holding any of <paramref name="userIds"/>, in one read, in no particular order.
    /// <para>
    /// An id no account holds is simply absent from the answer, so a caller naming the authors of a
    /// conversation learns which of them have since been deleted by what is missing. Asked as one
    /// question rather than one <see cref="GetByIdAsync"/> per id, so naming every sender costs one
    /// round trip however many there are.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserAccount>> ListByIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken);

    /// <summary>Whether any account already holds that email, compared as Identity normalizes it.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the account with its single role, hashing <paramref name="password"/> through
    /// Identity. The returned record carries the database-assigned id, which the caller
    /// needs for the subtype row's foreign key.
    /// </summary>
    /// <exception cref="Common.ConflictException">The email is already in use.</exception>
    /// <exception cref="Common.Errors.ValidationException">Identity's own policy refused the password.</exception>
    Task<UserAccount> CreateAsync(
        NewUserAccount account,
        string password,
        UserRole role,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages the account for deletion, so it lands with the rest of the operation at
    /// <see cref="IUnitOfWork.CommitAsync"/>.
    /// <para>
    /// Deleting the account rather than only the subtype row is the point: an account still holding
    /// role <c>Driver</c> with no driver row behind it is signable-in and carries a null driver
    /// claim, which is a worse state than either. The declared cascades do the rest — the driver
    /// row, its shifts and its chat messages go with it, and its deliveries are left unassigned.
    /// </para>
    /// <para>
    /// An id no account holds is not an error here: the caller has already loaded the row it means
    /// to delete and answered 404 if there was none.
    /// </para>
    /// </summary>
    Task DeleteAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="password"/> matches the stored hash for that user.</summary>
    Task<bool> VerifyPasswordAsync(UserId userId, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Ensures a role row exists for <paramref name="role"/>. Idempotent, and the only reason the
    /// role vocabulary needs a write path at all: the startup seeder creates the four rows
    /// once and nothing else ever adds one.
    /// </summary>
    Task EnsureRoleAsync(UserRole role, CancellationToken cancellationToken);

    /// <summary>
    /// Every account holding <paramref name="role"/>, ordered by name.
    /// <para>
    /// Unpaged, for the readers that need the whole set: a picker that must offer every client, or
    /// a lookup of names across a whole roster. The paged administration tables read through
    /// <see cref="ListDispatchersAsync"/> and the client repository's page instead.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserAccount>> ListByRoleAsync(UserRole role, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts behind the named driver rows, in no particular order. An id no driver holds is
    /// simply absent from the answer.
    /// <para>
    /// The keyed counterpart of <see cref="ListByRoleAsync"/>: a caller naming a handful of drivers
    /// - one delivery's, or one page's - reads those accounts rather than the whole roster.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserAccount>> ListByDriverIdsAsync(
        IReadOnlyCollection<DriverId> driverIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The accounts behind the named client rows, in no particular order. An id no client holds is
    /// simply absent from the answer.
    /// </summary>
    Task<IReadOnlyList<UserAccount>> ListByClientIdsAsync(
        IReadOnlyCollection<ClientId> clientIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The account's current Identity security stamp, or null when no account has that id.
    /// <para>
    /// Opaque, and read only to be compared: a session carries the stamp it was issued with, and a
    /// stamp that has since rolled - a password replaced - or an account that has gone means the
    /// session is no longer the account's. A stored null is answered as the empty string, so null
    /// keeps meaning "no account" and nothing else.
    /// </para>
    /// </summary>
    Task<string?> FindSecurityStampAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the dispatcher accounts, sorted in SQL over every one of them, and how many there
    /// are. Surname order when no column is named; every order ends on the id.
    /// </summary>
    /// <param name="query">The page and the direction. Already validated.</param>
    /// <param name="sort">The column, already parsed; null for surname order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<UserAccount>> ListDispatchersAsync(
        ListDispatchersQuery query,
        DispatcherSort? sort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renames an account, staged for the caller's commit.
    /// <para>
    /// Deliberately narrow: the two name fields and nothing else. The address is
    /// <see cref="SetEmailAsync"/>'s, the credentials are <see cref="SetPasswordAsync"/>'s, and an
    /// account holds one role for its whole life — a general "update the user" method here would
    /// be the seam through which a second writer of any of them appeared.
    /// </para>
    /// </summary>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    Task UpdateNameAsync(
        UserId userId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the stored password. The plaintext reaches storage only
    /// through Identity's hasher, and Identity's own strength policy runs here, so a weak password
    /// is refused by the same rule and reported with the same code as at registration.
    /// <para>
    /// It also rolls the security stamp, so every cookie session issued before the change is refused
    /// at its next revalidation (see <see cref="FindSecurityStampAsync"/>).
    /// </para>
    /// </summary>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    /// <exception cref="Common.Errors.ValidationException">Identity's own policy refused the password.</exception>
    Task SetPasswordAsync(UserId userId, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the address the account signs in with, staged for the caller's commit.
    /// <para>
    /// Deliberately narrow, for the reason <see cref="UpdateNameAsync"/> is: the address, the user name
    /// that mirrors it, and nothing else. A general "update the user" method here would be the seam
    /// through which a second writer of the address appeared.
    /// </para>
    /// <para>
    /// Staged by assignment on the tracked entity rather than through any <c>UserManager</c> update
    /// path — <see cref="SetPasswordAsync"/> spends the one such call a unit of work may make, and a
    /// single administrator's edit can carry both a password and an address.
    /// </para>
    /// <para>
    /// It does not ask whether the address is free: the caller has already asked, once, in the one
    /// place that decision lives. The race backstop is <c>user_name_index</c>, the unique index on
    /// <c>normalized_user_name</c> — <c>email_index</c> on <c>normalized_email</c> is declared
    /// without <c>unique</c> and enforces nothing. An implementation must therefore write the user
    /// name alongside the address: narrowed to the email columns alone, this method would have no
    /// backstop at all, and two callers claiming one address at the same instant would both be
    /// written. A collision that beats the check surfaces from the commit as
    /// <see cref="Common.ErrorCode.PERSISTENCE_UNIQUE_VIOLATION"/> — a 409, like the friendly answer.
    /// </para>
    /// </summary>
    /// <exception cref="Common.NotFoundException">No account has that id.</exception>
    Task SetEmailAsync(UserId userId, string email, CancellationToken cancellationToken);
}
