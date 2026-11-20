using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Dispatchers;

/// <inheritdoc cref="ClientAccountStateValidator" />
public sealed class DispatcherAccountStateValidator : AbstractValidator<DispatcherAccountState>
{
    /// <summary>Declares the rules.</summary>
    public DispatcherAccountStateValidator()
    {
        RuleFor(state => state.FirstName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_FIRST_NAME_TOO_LONG));

        RuleFor(state => state.LastName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_LAST_NAME_TOO_LONG));

        RuleFor(state => state.Password)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_WEAK))
            .MaximumLength(RegisterClientCommandValidator.PasswordMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_LONG))
            .When(state => state.Password is not null);
    }
}
