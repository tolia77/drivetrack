using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Authentication;

/// <summary>
/// The sign-in rules. Deliberately thin: anything beyond "both fields were sent" would tell an
/// unauthenticated caller something about the account they named, and the whole point of the
/// sign-in path is that a wrong email and a wrong password are indistinguishable.
/// <para>
/// No password-policy code here. An empty box is <c>COMMON_FIELD_REQUIRED</c>, not
/// <c>AUTH_PASSWORD_TOO_WEAK</c> - the policy sentence is advice for choosing a password, not for
/// entering one. The length ceiling the anonymous endpoint still needs is applied by
/// <see cref="UserService.SignInAsync"/> and answered as invalid credentials, because no stored
/// password can be longer than it.
/// </para>
/// </summary>
public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    /// <summary>Declares the rules.</summary>
    public SignInCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .MaximumLength(RegisterClientCommandValidator.EmailMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID));

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED));
    }
}
