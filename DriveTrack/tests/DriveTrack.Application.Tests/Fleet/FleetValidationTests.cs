using DriveTrack.Application.Common;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Vehicles;
using FluentValidation;
using FluentValidation.Results;
using DriveTrack.Application.Accounts.Authentication;

namespace DriveTrack.Application.Tests.Fleet;

/// <summary>
/// The vehicle and driver length rules judge the trimmed text, because the services store it
/// trimmed: a value of exactly the column's width with a trailing space fits once trimmed.
/// </summary>
public class FleetValidationTests
{
    private static readonly string Plate20 =
        new('A', CreateVehicleCommandValidator.LicensePlateMaximumLength);

    private static readonly string Plate21 =
        new('A', CreateVehicleCommandValidator.LicensePlateMaximumLength + 1);

    [Fact]
    public void A_full_width_plate_with_a_trailing_space_is_accepted_on_create()
    {
        Assert.Empty(Validate(new CreateVehicleCommandValidator(), Vehicle(Plate20 + " ")).Errors);
    }

    [Fact]
    public void A_plate_one_character_wider_than_the_column_is_refused_on_create()
    {
        AssertRefused(
            Validate(new CreateVehicleCommandValidator(), Vehicle(Plate21)),
            nameof(CreateVehicleCommand.LicensePlate),
            nameof(ErrorCode.FLEET_LICENSE_PLATE_TOO_LONG));
    }

    [Fact]
    public void A_full_width_plate_with_a_trailing_space_is_accepted_on_edit()
    {
        Assert.Empty(Validate(new UpdateVehicleCommandValidator(), VehicleEdit(Plate20 + " ")).Errors);
    }

    [Fact]
    public void A_plate_one_character_wider_than_the_column_is_refused_on_edit_naming_the_field()
    {
        AssertRefused(
            Validate(new UpdateVehicleCommandValidator(), VehicleEdit(Plate21)),
            nameof(UpdateVehicleCommand.LicensePlate),
            nameof(ErrorCode.FLEET_LICENSE_PLATE_TOO_LONG));
    }

    [Fact]
    public void A_full_width_model_with_a_trailing_newline_is_accepted()
    {
        var model = new string('я', CreateVehicleCommandValidator.ModelMaximumLength) + "\n";

        Assert.Empty(
            Validate(new CreateVehicleCommandValidator(), Vehicle("AA1234BB") with { Model = model })
                .Errors);
    }

    [Fact]
    public void A_plate_of_nothing_but_spaces_is_refused_as_missing()
    {
        AssertRefused(
            Validate(new CreateVehicleCommandValidator(), Vehicle("   ")),
            nameof(CreateVehicleCommand.LicensePlate),
            nameof(ErrorCode.COMMON_FIELD_REQUIRED));
    }

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

    private static CreateVehicleCommand Vehicle(string plate) => new("Фургон", plate, 1200m, 0, null);

    private static UpdateVehicleCommand VehicleEdit(string plate) =>
        new(
            Optional<string>.Present("Фургон"),
            Optional<string>.Present(plate),
            Optional<decimal>.Present(1200m),
            Optional<int>.Present(0),
            Optional<DateOnly?>.Present(null));

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
