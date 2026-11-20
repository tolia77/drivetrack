using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Domain.Reviews;
using FluentValidation;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// The same two rules, applied to the state the review will hold rather than to the payload.
/// <para>
/// This validator is only ever handed <c>UpdateReviewCommand.MergedOnto</c>'s result, in
/// which both fields are filled in from the stored row — which is what lets an edit that changes
/// only the rating pass without carrying the text again.
/// </para>
/// <para>
/// A second validator rather than a shared one over a shared type, because the two commands are
/// different things: a create names a delivery and an edit cannot. The rules themselves are stated
/// once — the bounds are <see cref="RatingScale"/>'s and the length is
/// <see cref="CreateReviewCommandValidator.TextMaximumLength"/> — so there is one answer to each
/// question even though there are two places that ask.
/// </para>
/// </summary>
public sealed class ReviewStateValidator : AbstractValidator<ReviewState>
{
    /// <summary>Declares the rules.</summary>
    public ReviewStateValidator()
    {
        RuleFor(state => state.Rating)
            .InclusiveBetween(RatingScale.Minimum, RatingScale.Maximum)
                .WithMessage(nameof(ErrorCode.REVIEW_RATING_OUT_OF_RANGE));

        RuleFor(state => state.Text)
            .NotEmpty().WithMessage(nameof(ErrorCode.REVIEW_TEXT_REQUIRED))
            .MaximumLength(CreateReviewCommandValidator.TextMaximumLength)
                .WithMessage(nameof(ErrorCode.REVIEW_TEXT_TOO_LONG));
    }
}
