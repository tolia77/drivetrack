using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using FluentValidation;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// The end-after-start rule over a shift dispatch is recording after the fact: a window may not run backwards.
/// <para>
/// The rule is stated on <c>EndedAt</c> rather than on the pair, because that is the field a
/// dispatcher entered wrongly — <c>StartedAt</c> is where the parallel rule lands on the edit path,
/// for the reason <see cref="ShiftStateValidator"/> gives. Validation promises <c>error.fields</c> keys a
/// form can attach a message to, so which property a rule hangs off decides which box gets marked.
/// </para>
/// <para>
/// Strictly greater, not greater-or-equal: a shift that started and ended at the same instant is a
/// stretch of time of no length, and recording one is a mistake rather than a fact.
/// </para>
/// <para>
/// There is nothing here about the driver or about a start in the future. The driver is a
/// <c>DriverId</c>, so the adapter refuses anything that is not a number at the model binder, and
/// the service refuses a driver no row holds. A future start with an end is a shift rostered ahead
/// and is allowed; a future start with no end is refused by <c>ShiftService</c>, which holds the
/// injected clock this validator does not.
/// </para>
/// <para>
/// Discovered by <c>AddValidatorsFromAssembly</c>, so there is no registration to add.
/// </para>
/// </summary>
public sealed class CreateShiftCommandValidator : AbstractValidator<CreateShiftCommand>
{
    /// <summary>Declares the rules.</summary>
    public CreateShiftCommandValidator() =>
        RuleFor(command => command.EndedAt)
            .Must((command, endedAt) => endedAt is null || endedAt > command.StartedAt)
                .WithMessage(nameof(ErrorCode.SHIFT_ENDED_BEFORE_STARTED));
}
