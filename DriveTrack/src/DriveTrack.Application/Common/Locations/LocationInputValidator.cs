using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Common.Locations;

/// <summary>
/// The ranges <see cref="MapLocation"/> enforces, restated where a failure can carry a field name.
/// The two are deliberately the same rule in two places: this one refuses politely, and
/// the constructor refuses absolutely, so a coordinate that reached the domain without passing
/// here would still not become a marker nobody can find.
/// </summary>
public sealed class LocationInputValidator : AbstractValidator<LocationInput>
{
    /// <summary>Declares the rules.</summary>
    public LocationInputValidator()
    {
        // Each range is written as a positive test on a value that is present, so a null is
        // reported once - by NotNull - rather than twice by two rules that disagree about whose
        // job it was. The comparison is a positive range rather than two negations for the reason
        // MapLocation's is a negated one: NaN fails every comparison, so `value >= -90` already
        // rejects it instead of letting it through as "not greater than 90".
        RuleFor(location => location.Latitude)
            .NotNull().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(latitude => latitude is not { } value || (value >= -90 && value <= 90))
                .WithMessage(nameof(ErrorCode.COMMON_COORDINATE_OUT_OF_RANGE));

        RuleFor(location => location.Longitude)
            .NotNull().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(longitude => longitude is not { } value || (value >= -180 && value <= 180))
                .WithMessage(nameof(ErrorCode.COMMON_COORDINATE_OUT_OF_RANGE));
    }
}
