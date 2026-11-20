using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Domain.Deliveries;
using FluentValidation;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// That a status was named at all, that it is one the system has, and that the note fits the column.
/// <para>
/// Whether <c>InTransit</c> may follow <c>Pending</c> is deliberately not judged here.
/// <see cref="Delivery.NextStatuses"/> is the single place the lifecycle is declared;
/// restating it as a validator rule would be the second copy that decision exists to prevent, and
/// it would report a 422 where an invalid transition is a 409. "Is this a member of the enum" is
/// not a transition rule — it is the same kind of claim as "is this field present".
/// </para>
/// <para>
/// Both of those claims have to be made here, because neither the binder nor the JSON reader makes
/// them: the shell registers <c>JsonStringEnumConverter</c> with its default
/// <c>allowIntegerValues: true</c>, so <c>{"status": 42}</c> deserializes to an undefined member
/// rather than being refused as malformed.
/// </para>
/// </summary>
public sealed class ChangeDeliveryStatusCommandValidator : AbstractValidator<ChangeDeliveryStatusCommand>
{
    /// <summary>Declares the rules.</summary>
    public ChangeDeliveryStatusCommandValidator()
    {
        RuleFor(command => command.Status)
            .NotNull().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .IsInEnum().WithMessage(nameof(ErrorCode.DELIVERY_STATUS_UNKNOWN));

        // Bounded only, and not required: a status change explains itself, and demanding a sentence
        // for every one would put "ok" in a thousand rows.
        // The rule lives on the entity that owns the column, so the standalone-note path applies the
        // same one without either validator depending on the other.
        RuleFor(command => command.Note)
            .Must(TimelineEntry.NoteFits).WithMessage(nameof(ErrorCode.DELIVERY_NOTE_TOO_LONG));
    }
}
