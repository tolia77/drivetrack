using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// The email address's shape rules — the same three checks, and the same code, as every other address in the
/// system. Whether the address is <em>free</em> is asked once, in <see cref="EmailChange"/>, because
/// it is a question about the whole roster rather than about this request.
/// </summary>
public sealed class ChangeEmailCommandValidator : AbstractValidator<ChangeEmailCommand>
{
    /// <summary>Declares the rules.</summary>
    public ChangeEmailCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .MaximumLength(RegisterClientCommandValidator.EmailMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .EmailAddress().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID));
    }
}
