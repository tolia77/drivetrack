using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Vehicles;
using FluentValidation;
using FluentValidation.Results;

namespace DriveTrack.Application.Tests.Drivers;

/// <summary>
/// The driver length rules judge the trimmed text, because the service stores it trimmed: a value of
/// exactly the column's width with a trailing space fits once trimmed.
/// </summary>
public class DriverValidationTests
{
    [Fact]
    public void Full_width_driver_fields_with_trailing_spaces_are_accepted_on_create()
    {
        var name = new string('я', RegisterClientCommandValidator.NameMaximumLength) + " ";
        var licence = new string('7', CreateDriverCommandValidator.LicenseNumberMaximumLength) + " ";

        Assert.Empty(
            Validate(
                new CreateDriverCommandValidator(),
                new CreateDriverCommand(name, name, "olena@drivetrack.test ", "New-Passw0rd", licence, null))
                .Errors);
    }

    [Fact]
    public void A_licence_number_one_character_wider_than_the_column_is_refused_on_edit()
    {
        var licence = new string('7', CreateDriverCommandValidator.LicenseNumberMaximumLength + 1);

        AssertRefused(
            Validate(new UpdateDriverCommandValidator(), DriverEdit(licence)),
            nameof(UpdateDriverCommand.LicenseNumber),
            nameof(ErrorCode.FLEET_LICENSE_NUMBER_TOO_LONG));
    }

    [Fact]
    public void A_full_width_licence_number_with_a_trailing_space_is_accepted_on_edit()
    {
        var licence = new string('7', CreateDriverCommandValidator.LicenseNumberMaximumLength) + " ";

        Assert.Empty(Validate(new UpdateDriverCommandValidator(), DriverEdit(licence)).Errors);
    }

    private static UpdateDriverCommand DriverEdit(string licence) =>
        new(
            Optional<string>.Present("Олена"),
            Optional<string>.Present("Петренко"),
            Optional<string>.Present(licence),
            Optional<int?>.Present(null));

    private static ValidationResult Validate<T>(IValidator<T> validator, T instance) =>
        validator.Validate(instance);

    private static void AssertRefused(ValidationResult result, string field, string key) =>
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == field && error.ErrorMessage == key);
}
