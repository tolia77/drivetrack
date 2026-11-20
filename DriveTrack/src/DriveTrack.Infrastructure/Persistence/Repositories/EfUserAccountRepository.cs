using System.Globalization;
using DriveTrack.Application.Abstractions.Persistence;
using DriveTrack.Application.Accounts.Dispatchers;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Drivers;
using DriveTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>
/// The Identity port over the unit of work's own context and its scoped managers.
/// <para>
/// Nothing Identity-shaped leaves this file: <c>ApplicationUser</c>, <c>IdentityResult</c> and the
/// managers all stop here, and Application sees <see cref="UserAccount"/> and the five typed
/// failures.
/// </para>
/// </summary>
internal sealed class EfUserAccountRepository(AppDbContext context, ScopedIdentity identity)
    : IUserAccountRepository
{
    /// <inheritdoc />
    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = identity.Users.NormalizeEmail(email);

        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == normalized, cancellationToken);

        return user is null ? null : await MapAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserAccount?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == id.Value, cancellationToken);

        return user is null ? null : await MapAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserAccount?> FindByClientIdAsync(
        ClientId clientId,
        CancellationToken cancellationToken)
    {
        // Two reads rather than a join, and the reason is the typed id. UserId reaches the
        // database through a value converter, so comparing a whole typed id translates - which is
        // how every other query here is written - while reaching through to its `.Value` inside an
        // expression tree does not. Materializing the client first turns the second comparison into
        // a plain captured integer.
        var client = await context.Clients
            .FirstOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);

        if (client is null)
        {
            return null;
        }

        var userId = client.UserId.Value;

        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        // Through the same mapper as every other read here, so the account a notification is
        // addressed to cannot disagree with the account the roster shows about its role or its
        // subtype id.
        return user is null ? null : await MapAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccount>> ListByIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Select(id => id.Value).Distinct().ToArray();

        return await ListAccountsAsync(
            context.Users.Where(user => ids.Contains(user.Id)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        // Compared as Identity stores it, not as the caller typed it: two addresses differing only
        // in case are one account, and asking the question any other way would let the second one
        // through as far as the unique index.
        var normalized = identity.Users.NormalizeEmail(email);

        return context.Users.AnyAsync(
            candidate => candidate.NormalizedEmail == normalized,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserAccount> CreateAsync(
        NewUserAccount account,
        string password,
        UserRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var user = new ApplicationUser
        {
            FirstName = account.FirstName,
            LastName = account.LastName,
            UserName = account.Email,
            Email = account.Email,
        };

        // The password reaches storage only through Identity's IPasswordHasher. This is the
        // one call in the system that turns a plaintext password into a stored value.
        Throw(await identity.Users.CreateAsync(user, password));

        // One flush, here, for two reasons that both come down to the key.
        //
        // The subtype row's foreign key needs the real number: the Client and Driver relationships
        // are raw SQL in the DomainModel migration (see IdentityModelConfiguration), because their
        // typed UserId cannot be declared against IdentityUser's plain int key - so EF has nothing
        // to propagate a temporary key through, and would write the placeholder.
        //
        // And AddToRoleAsync below re-attaches the user, which EF refuses while its key is still
        // temporary. Identity itself never decides when to save - AutoSaveChanges is off - so the
        // number and placement of saves is decided here, and this is the only one.
        //
        // It is a flush inside the unit of work's transaction, not a commit: the scope still owns
        // the transaction, and disposing it without committing still leaves nothing behind.
        await FlushAsync(cancellationToken);

        // Exactly one role row, and the unique index on asp_net_user_roles.user_id is what
        // makes a second one impossible rather than merely unusual. Staged, not saved - it reaches
        // the database with the subtype row at IUnitOfWork.CommitAsync.
        Throw(await identity.Users.AddToRoleAsync(user, role.ToString()));

        return new UserAccount(
            new UserId(user.Id),
            user.FirstName,
            user.LastName,
            user.Email!,
            role,
            DriverId: null,
            ClientId: null);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(UserId userId, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            // The caller loaded the row it means to delete and answered 404 if there was none, so
            // an id with nothing behind it here is a row that vanished between the two reads inside
            // one transaction. Deleting nothing is the correct outcome either way.
            return;
        }

        // Staged, not saved. identity.Users has AutoSaveChanges off, so the store marks the entity
        // Deleted and returns success without touching the database; the DELETE goes out with
        // everything else the operation wrote at IUnitOfWork.CommitAsync.
        //
        // The role row, the driver row, its shifts and its chat messages follow by the cascades the
        // schema declares, and the driver's deliveries are set to a null driver. The client
        // side is the same shape: the client row cascades from asp_net_users, its reviews cascade
        // from clients, its deliveries' client_id goes null, and the timeline, message and proof
        // actor references go null. None of that is restated here: a cascade written
        // twice is a cascade that can disagree with itself.
        Throw(await identity.Users.DeleteAsync(user));
    }

    /// <inheritdoc />
    public async Task<bool> VerifyPasswordAsync(
        UserId userId,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return false;
        }

        return await identity.Users.CheckPasswordAsync(user, password);
    }

    /// <inheritdoc />
    public async Task EnsureRoleAsync(UserRole role, CancellationToken cancellationToken)
    {
        var name = role.ToString();

        if (await identity.Roles.RoleExistsAsync(name))
        {
            return;
        }

        Throw(await identity.Roles.CreateAsync(new IdentityRole<int>(name)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccount>> ListByRoleAsync(
        UserRole role,
        CancellationToken cancellationToken)
    {
        var roleName = role.ToString();

        // One untracked read for the whole roster. Joined through the role rows rather than
        // filtered in memory, and the subtype ids are left-joined in the same statement rather than
        // read per user: the role is the WHERE clause's own answer, so MapAsync's per-row role and
        // subtype reads would only ask again what this query already established. A user has
        // at most one driver row and one client row, so neither join multiplies a row.
        var rows = await (
            from user in context.Users.AsNoTracking()
            join userRole in context.UserRoles on user.Id equals userRole.UserId
            join declared in context.Roles on userRole.RoleId equals declared.Id
            join driverRow in context.Drivers on user.Id equals (int)(object)driverRow.UserId into drivers
            from driver in drivers.DefaultIfEmpty()
            join clientRow in context.Clients on user.Id equals (int)(object)clientRow.UserId into clients
            from client in clients.DefaultIfEmpty()
            where declared.Name == roleName
            orderby user.LastName, user.FirstName, user.Id
            select new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                Driver = driver,
                Client = client,
            }).ToListAsync(cancellationToken);

        // The subtype id only for the role that has one, exactly as MapAsync reads it, so a list row
        // and a detail read of the same account can never disagree about it.
        return
        [
            .. rows.Select(row => new UserAccount(
                new UserId(row.Id),
                row.FirstName,
                row.LastName,
                row.Email!,
                role,
                role == UserRole.Driver ? row.Driver?.Id : null,
                role == UserRole.Client ? row.Client?.Id : null)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccount>> ListByDriverIdsAsync(
        IReadOnlyCollection<DriverId> driverIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(driverIds);

        if (driverIds.Count == 0)
        {
            return [];
        }

        var keys = driverIds.Distinct().ToArray();

        var rows = await context.Drivers
            .AsNoTracking()
            .Where(driver => keys.Contains(driver.Id))
            .Join(
                context.Users,
                driver => (int)(object)driver.UserId,
                user => user.Id,
                (driver, user) => new
                {
                    DriverId = driver.Id,
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                })
            .ToListAsync(cancellationToken);

        // The role is the driver row's own answer: only a Driver account has a driver row,
        // so a role read here would only confirm what the join already established.
        return
        [
            .. rows.Select(row => new UserAccount(
                new UserId(row.Id),
                row.FirstName,
                row.LastName,
                row.Email!,
                UserRole.Driver,
                row.DriverId,
                ClientId: null)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccount>> ListByClientIdsAsync(
        IReadOnlyCollection<ClientId> clientIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientIds);

        if (clientIds.Count == 0)
        {
            return [];
        }

        var keys = clientIds.Distinct().ToArray();

        var rows = await context.Clients
            .AsNoTracking()
            .Where(client => keys.Contains(client.Id))
            .Join(
                context.Users,
                client => (int)(object)client.UserId,
                user => user.Id,
                (client, user) => new
                {
                    ClientId = client.Id,
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                })
            .ToListAsync(cancellationToken);

        // As ListByDriverIdsAsync: a client row belongs only to a Client account.
        return
        [
            .. rows.Select(row => new UserAccount(
                new UserId(row.Id),
                row.FirstName,
                row.LastName,
                row.Email!,
                UserRole.Client,
                DriverId: null,
                row.ClientId)),
        ];
    }

    /// <inheritdoc />
    public async Task<string?> FindSecurityStampAsync(UserId userId, CancellationToken cancellationToken)
    {
        // Projected into a row so "no account" and "an account with no stamp" stay distinct: the
        // first is null, the second the empty string. Untracked, because nothing read on the
        // session-revalidation path is ever written back.
        var row = await context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId.Value)
            .Select(candidate => new { candidate.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : row.SecurityStamp ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<Paged<UserAccount>> ListDispatchersAsync(
        ListDispatchersQuery query,
        DispatcherSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var roleName = nameof(UserRole.Dispatcher);

        // Joined through the role rows, as ListByRoleAsync is, so the page and its total are both
        // answers about the join rather than about users filtered in memory.
        var users =
            from user in context.Users.AsNoTracking()
            join userRole in context.UserRoles on user.Id equals userRole.UserId
            join declared in context.Roles on userRole.RoleId equals declared.Id
            where declared.Name == roleName
            select user;

        var descending = query.Descending;

        var page = await users.ToPageAsync(
            rows => sort switch
            {
                DispatcherSort.FirstName => rows.SortBy(user => user.FirstName, descending)
                    .ThenSortBy(user => user.Id, descending),
                DispatcherSort.Email => rows.SortBy(user => user.Email, descending)
                    .ThenSortBy(user => user.Id, descending),
                _ => rows.SortBy(user => user.LastName, descending)
                    .ThenSortBy(user => user.FirstName, descending)
                    .ThenSortBy(user => user.Id, descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);

        // Mapped here rather than through MapAsync: the role is the WHERE clause's own answer and a
        // dispatcher has no subtype row, so a per-row role read would only ask the database
        // to confirm what this query already established.
        return page.Map(user => new UserAccount(
            new UserId(user.Id),
            user.FirstName,
            user.LastName,
            user.Email!,
            UserRole.Dispatcher,
            DriverId: null,
            ClientId: null));
    }

    /// <inheritdoc />
    public async Task UpdateNameAsync(
        UserId userId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var user = await RequiredAsync(userId, cancellationToken);

        // Staged on the tracked entity, and deliberately *not* through UserManager.UpdateAsync.
        //
        // That method calls Context.Attach first, and attaching an entity EF is already tracking as
        // Modified accepts its current values as its original values - including the concurrency
        // stamp the same method has just replaced. A second Identity update-path call in the same
        // unit of work therefore builds `WHERE concurrency_stamp = <the stamp we invented>`, matches
        // no row, and the commit fails as a concurrency conflict on a row nobody else touched. Since
        // an account edit can change a name and a password in one operation, one such call per unit of
        // work is the budget, and SetPasswordAsync below spends it.
        //
        // Nothing is lost by staging directly: the commit writes these columns with the loaded stamp
        // in the WHERE clause, so the optimistic check is still the real one. And nothing is
        // wanted from UpdateAsync either: it would roll the security stamp and re-validate the whole
        // user for a change to two name columns.
        user.FirstName = firstName;
        user.LastName = lastName;
    }

    /// <inheritdoc />
    public async Task SetPasswordAsync(
        UserId userId,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await RequiredAsync(userId, cancellationToken);

        // AddPasswordAsync is the call that matters: it runs Identity's strength policy and its
        // IPasswordHasher, so a weak password is refused by the same rule and reported with the same
        // code as at registration, and only the hash is ever stored.
        //
        // It also rolls the security stamp, and that is what ends the account's cookie sessions: the
        // cookie carries the stamp it was issued with, and the Web adapter's revalidation refuses
        // one that no longer matches. A bearer token carries no stamp and runs to its own expiry.
        //
        // AddPasswordAsync refuses a user who already has a password, so the stored hash is cleared
        // first - by assignment rather than by RemovePasswordAsync. That is not a shortcut:
        // RemovePasswordAsync is a second Identity update-path call, and two of them in one unit of
        // work corrupt the concurrency stamp for the reason UpdateNameAsync above sets out. The
        // assignment writes nothing on its own; the hash that reaches the database is the one
        // AddPasswordAsync computes, at the unit of work's single commit.
        //
        // The precondition that follows from clearing it: once this method has been entered, the
        // tracked user carries no password until AddPasswordAsync succeeds. A caller that catches
        // the refusal must therefore abandon the unit of work rather than commit it - committing
        // would store the null and leave an account nobody can sign in to. Every caller here does
        // abandon it, because the exception propagates out of the service and the scope is disposed
        // without a commit, which rolls the transaction back.
        user.PasswordHash = null;

        Throw(await identity.Users.AddPasswordAsync(user, password));
    }

    /// <inheritdoc />
    public async Task SetEmailAsync(UserId userId, string email, CancellationToken cancellationToken)
    {
        var user = await RequiredAsync(userId, cancellationToken);

        // Staged on the tracked entity, for the reason UpdateNameAsync sets out at length:
        // UserManager.SetEmailAsync and SetUserNameAsync are both Identity update-path calls, and
        // SetPasswordAsync above already spends the one such call a unit of work may make. An
        // administrator's single PATCH can carry a password and an address at once, so this side
        // has to be the assignment.
        //
        // Four columns, and the user-name pair is the load-bearing half.
        //
        // `email_index` on normalized_email is declared WITHOUT `unique` (see the DomainModel
        // migration); the only unique index on this table's identity columns is `user_name_index` on
        // normalized_user_name. So the user-name pair is the entire race backstop behind
        // EmailChange's friendly EmailExistsAsync check - narrow this method to the email columns
        // and two callers claiming one address at the same instant would both be written.
        //
        // Registration sets UserName = Email, so writing only the email pair would also leave the
        // user name pointing at an address the account no longer has.
        //
        // Normalized through identity.Users so the stored form is exactly what EmailExistsAsync and
        // FindByEmailAsync compare against - a hand-rolled ToUpperInvariant here would be a second
        // normalizer that could disagree with Identity's own.
        user.Email = email;
        user.NormalizedEmail = identity.Users.NormalizeEmail(email);
        user.UserName = email;
        user.NormalizedUserName = identity.Users.NormalizeName(email);
    }

    /// <summary>
    /// Loads a user that must exist. The services have already refused an unknown id, so this
    /// answers the case where a row disappeared between the read and the write - which is a 404
    /// rather than a null reference two lines later.
    /// </summary>
    private async Task<ApplicationUser> RequiredAsync(UserId userId, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);

        return user ?? throw new NotFoundException(
            ErrorCode.COMMON_NOT_FOUND,
            "No user exists with id " + userId.Value.ToString(CultureInfo.InvariantCulture) + ".");
    }

    /// <summary>
    /// Saves the staged changes inside the unit of work's transaction, translating a constraint
    /// refusal exactly as <c>UnitOfWork.CommitAsync</c> does, so <c>user_name_index</c> - unique on
    /// the normalized user name, which is the email - surfaces as a 409 here or at the commit.
    /// </summary>
    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var translated = PostgresConstraintTranslator.Translate(exception);

            if (ReferenceEquals(translated, exception))
            {
                throw;
            }

            throw translated;
        }
    }

    /// <summary>
    /// Every account <paramref name="users"/> selects, each with its role and subtype row id, in one
    /// untracked round trip. The role and the subtype ids are correlated subqueries rather than
    /// reads per row, and they follow <see cref="MapAsync"/>'s rules exactly - one recognised role,
    /// and only the subtype id that role has - so a batch read and a single read of the same account
    /// can never disagree about either.
    /// </summary>
    private async Task<IReadOnlyList<UserAccount>> ListAccountsAsync(
        IQueryable<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var rows = await users
            .AsNoTracking()
            .Select(user => new AccountRow
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email!,
                RoleName = (
                    from userRole in context.UserRoles
                    join declared in context.Roles on userRole.RoleId equals declared.Id
                    where userRole.UserId == user.Id
                    select declared.Name).FirstOrDefault(),
                DriverId = context.Drivers
                    .Where(driver => (int)(object)driver.UserId == user.Id)
                    .Select(driver => (DriverId?)driver.Id)
                    .FirstOrDefault(),
                ClientId = context.Clients
                    .Where(client => (int)(object)client.UserId == user.Id)
                    .Select(client => (ClientId?)client.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row =>
            {
                var role = ParseRole(row.Id, row.RoleName);

                return new UserAccount(
                    new UserId(row.Id),
                    row.FirstName,
                    row.LastName,
                    row.Email,
                    role,
                    role == UserRole.Driver ? row.DriverId : null,
                    role == UserRole.Client ? row.ClientId : null);
            }),
        ];
    }

    /// <summary>
    /// The role a user's single role row names. One role, drawn from the four, a
    /// schema-level invariant. A row that breaks it is a defect, not a caller error, so it leaves as
    /// the 500 envelope rather than being smoothed over into a plausible-looking profile.
    /// </summary>
    private static UserRole ParseRole(int userId, string? roleName) =>
        roleName is not null && Enum.TryParse<UserRole>(roleName, ignoreCase: false, out var role)
            ? role
            : throw new InvalidOperationException(
                "User " + userId.ToString(CultureInfo.InvariantCulture)
                    + " holds no recognised role; exactly one is required.");

    /// <summary>Reads the single role row and the subtype row id for a loaded user.</summary>
    private async Task<UserAccount> MapAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roleName = await (
            from userRole in context.UserRoles
            join declared in context.Roles on userRole.RoleId equals declared.Id
            where userRole.UserId == user.Id
            select declared.Name).FirstOrDefaultAsync(cancellationToken);

        var role = ParseRole(user.Id, roleName);

        var userId = new UserId(user.Id);
        DriverId? driverId = null;
        ClientId? clientId = null;

        if (role == UserRole.Driver)
        {
            var driver = await context.Drivers
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            driverId = driver?.Id;
        }
        else if (role == UserRole.Client)
        {
            var client = await context.Clients
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            clientId = client?.Id;
        }

        return new UserAccount(
            userId,
            user.FirstName,
            user.LastName,
            user.Email!,
            role,
            driverId,
            clientId);
    }

    /// <summary>
    /// Turns an <c>IdentityResult</c> failure into one of the typed failures. Identity's own
    /// error codes are the input, the contract's codes are the output, and the mapping lives here
    /// so no caller ever inspects an <c>IdentityError</c>.
    /// </summary>
    private static void Throw(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = result.Errors.Select(error => error.Code).ToArray();
        var detail = string.Join(", ", result.Errors.Select(error => error.Code + ": " + error.Description));

        if (Array.Exists(
                codes,
                code => code.Equals("DuplicateEmail", StringComparison.Ordinal)
                    || code.Equals("DuplicateUserName", StringComparison.Ordinal)))
        {
            throw new ConflictException(ErrorCode.AUTH_EMAIL_ALREADY_IN_USE, detail);
        }

        if (Array.Exists(codes, code => code.StartsWith("Password", StringComparison.Ordinal)))
        {
            throw new ValidationException(
                ErrorCode.COMMON_VALIDATION_FAILED,
                detail,
                [new FieldError("Password", nameof(ErrorCode.AUTH_PASSWORD_TOO_WEAK))]);
        }

        if (Array.Exists(
                codes,
                code => code.Equals("InvalidEmail", StringComparison.Ordinal)
                    || code.Equals("InvalidUserName", StringComparison.Ordinal)))
        {
            throw new ValidationException(
                ErrorCode.COMMON_VALIDATION_FAILED,
                detail,
                [new FieldError("Email", nameof(ErrorCode.AUTH_EMAIL_INVALID))]);
        }

        throw new ValidationException(ErrorCode.COMMON_VALIDATION_FAILED, detail, []);
    }

    /// <summary>The shape a batch account read is projected into: an initializer, so EF can see through it.</summary>
    private sealed class AccountRow
    {
        public int Id { get; init; }

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string? RoleName { get; init; }

        public DriverId? DriverId { get; init; }

        public ClientId? ClientId { get; init; }
    }
}
