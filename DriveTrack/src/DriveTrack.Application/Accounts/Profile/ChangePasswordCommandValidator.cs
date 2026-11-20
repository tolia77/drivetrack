using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// The password-change rules. Presence and bounds only on the two passwords, plus the confirmation: the strength
/// policy itself is Identity's, configured once in <c>AddIdentityCore</c> and reported back through
/// the same code, so the two cannot disagree.
/// <para>
/// Whether the current password is <em>right</em> is not a question a validator can answer — only a
/// hash is stored — so that check lives in the service, against the repository, and raises
/// <see cref="ErrorCode.AUTH_CURRENT_PASSWORD_INCORRECT"/>.
/// </para>
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>Declares the rules.</summary>
    public ChangePasswordCommandValidator()
    {
        // Neither rule is the strength code: an absent current password is a field the caller left
        // out, and one over the ceiling is long rather than weak. AUTH_PASSWORD_TOO_WEAK names the
        // policy - eight characters, both cases, a digit - which is advice about neither.
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.PasswordMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_LONG));

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_WEAK))
            .MaximumLength(RegisterClientCommandValidator.PasswordMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_LONG));

        RuleFor(command => command.NewPasswordConfirmation)
            .Equal(command => command.NewPassword)
            .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_CONFIRMATION_MISMATCH));
    }
}
