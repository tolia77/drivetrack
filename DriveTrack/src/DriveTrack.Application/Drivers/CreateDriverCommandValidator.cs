using DriveTrack.Application.Common;
using DriveTrack.Application.Users;
using FluentValidation;

namespace DriveTrack.Application.Drivers;

/// <summary>
/// The rules for taking on a driver.
/// <para>
/// Every message is an <see cref="ErrorCode"/> name applied per rule, never once at the end of a
/// chain. The account bounds are shared with <see cref="RegisterClientCommandValidator"/> rather
/// than restated: the same columns are being written, and two copies of a length would be two
/// answers to one question.
/// </para>
/// <para>
/// The stored text fields are judged trimmed, because <c>DriverService</c> trims them before it
/// writes: a name of exactly the column's width with a trailing space fits once trimmed and must not
/// be refused. <c>NotEmpty</c> already refuses whitespace-only text, so only the length rules need
/// the trim. The password is judged as sent, because it is passed on as sent.
/// </para>
/// </summary>
public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    /// <summary>Matching the <c>drivers.license_number</c> column.</summary>
    public const int LicenseNumberMaximumLength = 50;

    /// <summary>Declares the rules.</summary>
    public CreateDriverCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(value => FitsTrimmed(value, RegisterClientCommandValidator.NameMaximumLength))
                .WithMessage(nameof(ErrorCode.AUTH_FIRST_NAME_TOO_LONG));

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(value => FitsTrimmed(value, RegisterClientCommandValidator.NameMaximumLength))
                .WithMessage(nameof(ErrorCode.AUTH_LAST_NAME_TOO_LONG));

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .Must(value => FitsTrimmed(value, RegisterClientCommandValidator.EmailMaximumLength))
                .WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID))
            .EmailAddress().WithMessage(nameof(ErrorCode.AUTH_EMAIL_INVALID));

        // Presence and a ceiling, keyed apart: the strength policy is Identity's, configured once
        // in AddIdentityCore and reported back through AUTH_PASSWORD_TOO_WEAK, and a password over
        // the ceiling is long rather than weak.
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_WEAK))
            .MaximumLength(RegisterClientCommandValidator.PasswordMaximumLength)
                .WithMessage(nameof(ErrorCode.AUTH_PASSWORD_TOO_LONG));

        RuleFor(command => command.LicenseNumber)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(value => FitsTrimmed(value, LicenseNumberMaximumLength))
                .WithMessage(nameof(ErrorCode.FLEET_LICENSE_NUMBER_TOO_LONG));

        // Nothing about VehicleId: whether that vehicle exists and whether it is free are questions
        // about other rows, and they are answered in the one writer of drivers.vehicle_id.
    }

    /// <summary>Whether the value, trimmed as the service will store it, fits within the bound.</summary>
    internal static bool FitsTrimmed(string? value, int maximumLength) =>
        value is null || value.Trim().Length <= maximumLength;
}
