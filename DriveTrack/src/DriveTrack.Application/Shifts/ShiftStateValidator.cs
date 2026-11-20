using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// The same rule, applied to the state the shift will hold rather than to the payload.
/// <para>
/// This validator is only ever handed <c>UpdateShiftCommand.MergedOnto</c>'s result, in which
/// both fields are filled in from the stored row. That is what makes the case the rule is most easily
/// regressed by a refusal: <c>PUT {"startedAt": T+9h}</c> against a shift of <c>[T, T+8h]</c>
/// carries no end at all, so a validator reading the payload would see one instant and nothing to
/// compare it with.
/// </para>
/// <para>
/// One predicate, stated twice — once against each end of the window — and that is about which box
/// a form marks rather than about the rule. A merged state can be reached from either side: an edit
/// that moves only the start, and an edit that moves only the end. Hanging the rule off one property
/// would answer every backwards window by naming that one, so half the callers would be told the
/// box they never touched was wrong while the box they did touch stayed unflagged. Naming
/// both is the honest answer, because a window that runs backwards really is a disagreement between
/// two values rather than a fault in either.
/// </para>
/// <para>
/// The create path states it once, off <c>EndedAt</c>, and that is not an inconsistency: a create
/// always carries both instants and the end is the one a dispatcher got wrong, so there is no second
/// caller for a second name to serve.
/// </para>
/// </summary>
public sealed class ShiftStateValidator : AbstractValidator<ShiftState>
{
    /// <summary>Declares the rules.</summary>
    public ShiftStateValidator()
    {
        RuleFor(state => state.StartedAt)
            .Must((state, startedAt) => state.EndedAt is null || state.EndedAt > startedAt)
                .WithMessage(nameof(ErrorCode.SHIFT_ENDED_BEFORE_STARTED));

        RuleFor(state => state.EndedAt)
            .Must((state, endedAt) => endedAt is null || endedAt > state.StartedAt)
                .WithMessage(nameof(ErrorCode.SHIFT_ENDED_BEFORE_STARTED));
    }
}
