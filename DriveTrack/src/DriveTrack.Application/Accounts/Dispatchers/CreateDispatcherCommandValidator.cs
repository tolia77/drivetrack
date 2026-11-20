using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Dispatchers;

/// <summary>
/// The rules for opening a dispatcher account. Not a merge: there is nothing stored yet, so
/// every field is required and the command is what gets validated.
/// <para>
/// There is no phone number. A dispatcher account is name, email and password, and a dispatcher has
/// no subtype row for a phone number to live in.
/// </para>
/// </summary>
public sealed class CreateDispatcherCommandValidator : AbstractValidator<CreateDispatcherCommand>
{
    /// <summary>Declares the rules.</summary>
    public CreateDispatcherCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_FIRST_NAME_TOO_LONG));

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_LAST_NAME_TOO_LONG));

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .MaximumLength(RegisterClientCommandValidator.EmailMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .EmailAddress().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID));

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_WEAK))
            .MaximumLength(RegisterClientCommandValidator.PasswordMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_LONG));
    }
}
