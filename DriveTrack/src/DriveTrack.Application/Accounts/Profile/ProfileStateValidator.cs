using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Profile;

/// <summary>
/// The profile-edit rules, held over the <em>merged</em> profile rather than over the payload.
/// <para>
/// The phone number is the whole reason <see cref="ProfileState.IsClient"/> is on the record. A
/// dispatcher, a driver and an administrator have no row to store a number in, so for them the
/// only legal merged value is none at all — and a caller who sent one is told so rather than having
/// the field silently dropped, which is the failure <see cref="Optional{T}"/> exists to prevent.
/// </para>
/// <para>
/// The limits and the E.164 pattern come from <see cref="RegisterClientCommandValidator"/> rather
/// than being restated: one phone rule for the whole system.
/// </para>
/// </summary>
public sealed class ProfileStateValidator : AbstractValidator<ProfileState>
{
    /// <summary>Declares the rules.</summary>
    public ProfileStateValidator()
    {
        RuleFor(state => state.FirstName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_FIRST_NAME_TOO_LONG));

        RuleFor(state => state.LastName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RegisterClientCommandValidator.NameMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_LAST_NAME_TOO_LONG));

        // A client must end up with a number: clearing one is not an operation registration leaves open, so
        // a present-null is a 422 rather than an empty string in the column.
        RuleFor(state => state.PhoneNumber)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_PHONE_NUMBER_INVALID))
            .MaximumLength(RegisterClientCommandValidator.PhoneNumberMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PHONE_NUMBER_INVALID))
            .Matches(RegisterClientCommandValidator.PhoneNumberPattern)
                .WithMessage(nameof(ErrorCode.AUTH_PHONE_NUMBER_INVALID))
            .When(state => state.IsClient);

        // And nobody else may end up with one. Same code as the rules above, because the caller's
        // next move is the same either way: stop sending that field.
        RuleFor(state => state.PhoneNumber)
            .Null().WithMessage(nameof(ErrorCode.AUTH_PHONE_NUMBER_INVALID))
            .When(state => !state.IsClient);
    }
}
