using DriveTrack.Application.Accounts.Authentication;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Vehicles;
using FluentValidation;
using FluentValidation.Results;

namespace DriveTrack.Application.Tests.Vehicles;

/// <summary>
/// The vehicle length rules judge the trimmed text, because the service stores it trimmed: a value of
/// exactly the column's width with a trailing space fits once trimmed.
/// </summary>
public class VehicleValidationTests
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

    private static CreateVehicleCommand Vehicle(string plate) => new("Фургон", plate, 1200m, 0, null);

    private static UpdateVehicleCommand VehicleEdit(string plate) =>
        new(
            Optional<string>.Present("Фургон"),
            Optional<string>.Present(plate),
            Optional<decimal>.Present(1200m),
            Optional<int>.Present(0),
            Optional<DateOnly?>.Present(null));

    private static ValidationResult Validate<T>(IValidator<T> validator, T instance) =>
        validator.Validate(instance);

    private static void AssertRefused(ValidationResult result, string field, string key) =>
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == field && error.ErrorMessage == key);
}
