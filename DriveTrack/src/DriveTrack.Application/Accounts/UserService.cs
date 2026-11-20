using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Accounts.Profile;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Identity;
using FluentValidation;

namespace DriveTrack.Application.Accounts;

/// <summary>
/// The fixed operation pipeline, three times over: authenticate (the adapter has already done that and handed
/// this layer an <see cref="ICurrentUser"/>) → load → guard → validate → act → commit → map.
/// </summary>
public sealed class UserService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IAccessGuard accessGuard,
    IAccessTokenIssuer accessTokenIssuer,
    IValidator<RegisterClientCommand> registerValidator,
    IValidator<SignInCommand> signInValidator,
    IValidator<ProfileState> profileValidator,
    IValidator<ChangePasswordCommand> passwordValidator,
    IValidator<ChangeEmailCommand> emailValidator) : IUserService
{
    /// <inheritdoc />
    /// <remarks>
    /// Allowlisted in <see cref="PublicEntryPoints"/>: the caller has no account yet, so there is
    /// nothing for a guard to judge.
    /// </remarks>
    public async Task<AuthenticatedSession> RegisterClientAsync(
        RegisterClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Trimmed before it is judged, as the profile edit and the dispatcher create are: the
        // validator has to see the strings that will be stored, or a padded phone number fails the
        // E.164 rule and a name of exactly the maximum length plus a space is refused for a length
        // the column never sees. The passwords are left alone - their spaces are part of them.
        var trimmed = command with
        {
            FirstName = Trim(command.FirstName),
            LastName = Trim(command.LastName),
            Email = Trim(command.Email),
            PhoneNumber = Trim(command.PhoneNumber),
        };

        await registerValidator.EnsureValidAsync(trimmed, cancellationToken);

        // The validator has proved these are present; the nullable annotations exist because the
        // wire can send anything and the command has to be able to carry it as far as here.
        var email = trimmed.Email!;

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        // The friendly answer. The race backstop behind it is user_name_index, unique on the
        // normalized user name - which is the email, because registration sets UserName = Email. It
        // surfaces as PERSISTENCE_UNIQUE_VIOLATION, also a 409, so two callers registering the same
        // address at the same instant get the same status either way.
        if (await unitOfWork.Users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException(
                ErrorCode.AUTH_EMAIL_ALREADY_IN_USE,
                "An account already exists for the submitted email address.");
        }

        var account = await unitOfWork.Users.CreateAsync(
            new NewUserAccount(trimmed.FirstName!, trimmed.LastName!, email),
            trimmed.Password!,
            UserRole.Client,
            cancellationToken);

        var client = new Client
        {
            UserId = account.Id,
            PhoneNumber = trimmed.PhoneNumber!,
        };

        unitOfWork.Clients.Add(client);

        // Read inside the transaction: CreateAsync has already flushed the user row it stamped.
        var securityStamp = await unitOfWork.Users.FindSecurityStampAsync(account.Id, cancellationToken);

        // The user row, its role row and the client row reach the database here or nowhere.
        await unitOfWork.CommitAsync(cancellationToken);

        // The client row's own key is assigned by the commit above, so the token can carry it.
        var registered = account with { ClientId = client.Id };

        return Session(registered, securityStamp);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Allowlisted in <see cref="PublicEntryPoints"/>: this is how credentials are presented in the
    /// first place, so there is no caller for a guard to read.
    /// </remarks>
    public async Task<AuthenticatedSession> SignInAsync(
        SignInCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await signInValidator.EnsureValidAsync(command, cancellationToken);

        // The length ceiling, answered as the wrong password it is: every path that sets a password
        // caps it at this length, so no stored hash can match a longer one. Refused before the
        // lookup and the hasher, because this endpoint is anonymous and the hasher deliberately slow.
        if (command.Password!.Length > RegisterClientCommandValidator.PasswordMaximumLength)
        {
            throw InvalidCredentials();
        }

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var account = await unitOfWork.Users.FindByEmailAsync(command.Email!.Trim(), cancellationToken);

        // One answer for an unknown address and for a wrong password. Distinguishing them would let
        // an attacker enumerate accounts, so both raise the same code and the same 401 body.
        if (account is null)
        {
            throw InvalidCredentials();
        }

        if (!await unitOfWork.Users.VerifyPasswordAsync(account.Id, command.Password!, cancellationToken))
        {
            throw InvalidCredentials();
        }

        var securityStamp = await unitOfWork.Users.FindSecurityStampAsync(account.Id, cancellationToken);

        // Nothing was written, so the scope is disposed without a commit and the empty transaction
        // rolls back. That is the ordinary read path, not an omission.
        return Session(account, securityStamp);
    }

    /// <inheritdoc />
    public async Task<UserProfile> GetProfileAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        // Load, then guard. The row is read before the decision so a future rule can be about
        // the row; nothing about it is disclosed unless the guard passes.
        var account = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        accessGuard.RequireSelf(userId);

        if (account is null)
        {
            throw NotFoundException.For("user", userId.Value);
        }

        // The licence number and the phone number are the two fields a subtype row
        // contributes, one for drivers and one for clients.
        var licenseNumber = await LicenseNumberAsync(unitOfWork, account, cancellationToken);
        var client = await ClientOrNullAsync(unitOfWork, account, cancellationToken);

        return new UserProfile(
            account.Id,
            account.FirstName,
            account.LastName,
            account.Email,
            account.Role,
            licenseNumber,
            client?.PhoneNumber);
    }

    /// <inheritdoc />
    public async Task<UserProfile> UpdateProfileAsync(
        UserId userId,
        UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var account = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        // Lexically here, in this method's own body: GuardCoverageTests reads the IL, and a private
        // helper that called the guard would not count.
        accessGuard.RequireSelf(userId);

        if (account is null)
        {
            throw NotFoundException.For("user", userId.Value);
        }

        var licenseNumber = await LicenseNumberAsync(unitOfWork, account, cancellationToken);
        var client = await ClientOrNullAsync(unitOfWork, account, cancellationToken);

        // The payload is merged onto what is stored, and the *merged* record is what the
        // validator judges - so a body carrying only a first name is not a phone number the caller
        // failed to send, it is one they left alone.
        //
        // Trimmed as it is merged, not afterwards: the validator has to see the value that will
        // actually be stored, or a name of exactly the maximum length with a trailing space is
        // refused for being one character too long when the string that reaches the column fits.
        //
        // A caller with no client row falls back to null, so a phone number they did send is the
        // only thing left in the field and the merged state is what refuses it.
        var merged = new ProfileState(
            Trim(command.FirstName.Or(account.FirstName)),
            Trim(command.LastName.Or(account.LastName)),
            Trim(command.PhoneNumber.Or(client?.PhoneNumber)),
            client is not null);

        await profileValidator.EnsureValidAsync(merged, cancellationToken);

        var firstName = merged.FirstName!;
        var lastName = merged.LastName!;

        await unitOfWork.Users.UpdateNameAsync(userId, firstName, lastName, cancellationToken);

        if (client is not null)
        {
            client.PhoneNumber = merged.PhoneNumber!;
        }

        // The renamed user and the edited client row reach the database here or nowhere.
        await unitOfWork.CommitAsync(cancellationToken);

        return new UserProfile(
            account.Id,
            firstName,
            lastName,
            account.Email,
            account.Role,
            licenseNumber,
            client?.PhoneNumber);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(
        UserId userId,
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var account = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        accessGuard.RequireSelf(userId);

        if (account is null)
        {
            throw NotFoundException.For("user", userId.Value);
        }

        await passwordValidator.EnsureValidAsync(command, cancellationToken);

        // Asked before anything is staged, and never trimmed: leading and trailing space is part of
        // a password somebody chose.
        if (!await unitOfWork.Users.VerifyPasswordAsync(
                userId, command.CurrentPassword!, cancellationToken))
        {
            // 422, not 401. The caller is signed in and it is one field of their request that was
            // wrong; a 401 would reach the session-expiry boundary as an expired session and sign them out.
            // Qualified: FluentValidation declares a ValidationException of its own, and the one
            // that carries an ErrorCode is the only one the adapter can turn into a 422.
            throw new Common.ValidationException(
                ErrorCode.AUTH_CURRENT_PASSWORD_INCORRECT,
                "The current password offered with a password change does not match the stored hash.",
                []);
        }

        // The plaintext reaches storage only through Identity's hasher, and Identity's own
        // strength policy runs in there - so a weak new password is refused by the same rule and
        // reported with the same code as at registration.
        await unitOfWork.Users.SetPasswordAsync(userId, command.NewPassword!, cancellationToken);

        // SetPasswordAsync clears the stored hash before it computes the new one, so a refusal must
        // leave without committing. The exception propagates and the scope is disposed unwritten;
        // this line is only reached when the hash exists.
        await unitOfWork.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserProfile> ChangeEmailAsync(
        UserId userId,
        ChangeEmailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var account = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        accessGuard.RequireSelf(userId);

        if (account is null)
        {
            throw NotFoundException.For("user", userId.Value);
        }

        // Trimmed before it is judged, exactly as the administrator's path trims as it merges: the
        // validator has to see the string that will actually be stored, or an address of exactly the
        // maximum length with a trailing space is refused here and accepted there - the asymmetry
        // EmailChange exists to prevent, moved one layer up into the validator.
        var requested = command with { Email = Trim(command.Email) };

        await emailValidator.EnsureValidAsync(requested, cancellationToken);

        var licenseNumber = await LicenseNumberAsync(unitOfWork, account, cancellationToken);
        var client = await ClientOrNullAsync(unitOfWork, account, cancellationToken);

        // The email uniqueness rule is asked in one place, by this path and by the administrator's.
        var email = await EmailChange.ApplyAsync(
            unitOfWork, account, requested.Email!, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return new UserProfile(
            account.Id,
            account.FirstName,
            account.LastName,
            email,
            account.Role,
            licenseNumber,
            client?.PhoneNumber);
    }

    /// <summary>
    /// Trims a merged field, leaving null alone so a present-null still reaches the validator as the
    /// empty answer it is rather than as an empty string.
    /// </summary>
    private static string? Trim(string? value) => value?.Trim();

    /// <summary>The driving licence number for a driver, and null for every other role.</summary>
    private static async Task<string?> LicenseNumberAsync(
        IUnitOfWork unitOfWork,
        UserAccount account,
        CancellationToken cancellationToken)
    {
        if (account.Role != UserRole.Driver || account.DriverId is not { } driverId)
        {
            return null;
        }

        var driver = await unitOfWork.Drivers.GetByIdAsync(driverId, cancellationToken);

        return driver?.LicenseNumber;
    }

    /// <summary>
    /// The client row behind an account, or null when the account is not a client. The row is
    /// part of what a client account is, so it is also the answer to "may this caller have a
    /// phone number at all".
    /// </summary>
    private static async Task<Client?> ClientOrNullAsync(
        IUnitOfWork unitOfWork,
        UserAccount account,
        CancellationToken cancellationToken)
    {
        if (account.ClientId is not { } clientId)
        {
            return null;
        }

        return await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken);
    }

    private static ForbiddenException InvalidCredentials() =>
        new(ErrorCode.AUTH_INVALID_CREDENTIALS, "Sign-in was refused: unknown email or wrong password.");

    private AuthenticatedSession Session(UserAccount account, string? securityStamp) =>
        new(
            account.Id,
            account.FirstName,
            account.LastName,
            account.Email,
            account.Role,
            accessTokenIssuer.Issue(account),
            account.DriverId,
            account.ClientId)
        {
            SecurityStamp = securityStamp,
        };
}
