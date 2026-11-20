using DriveTrack.Application.Common;
using DriveTrack.Domain.Reviews;
using FluentValidation;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// The rating scale and the text rule, over a review being written.
/// <para>
/// Both rules are judged over the <em>trimmed</em> text, because trimming is what the service
/// stores: a comment of exactly 2000 characters with a trailing newline would otherwise be refused
/// for being one character too long when the string that reaches the column fits. (<c>NotEmpty</c>
/// refuses whitespace-only text either way.) The trim belongs to the two contracts —
/// <see cref="CreateReviewCommand.Trimmed"/> and <see cref="UpdateReviewCommand.MergedOnto"/> — so
/// both write paths judge the same content the same way; this class states the rules and the caller
/// states which string they are about.
/// </para>
/// <para>
/// The bounds are <see cref="RatingScale"/>'s rather than two literals: the
/// scale is a product rule in Domain, read here and by the band the screen renders. They also
/// restate <c>CK_Reviews_Rating</c> — the column is the authority and a race cannot slip a 6 past
/// it, but stating the bound here is what makes the answer a 422 naming the field either way.
/// </para>
/// <para>
/// There is no rule about the rating being a whole number, and nothing is missing: it is an
/// <see cref="int"/>, so a fractional value cannot be carried as far as here — the adapter refuses
/// it at the model binder.
/// </para>
/// <para>
/// Discovered by <c>AddValidatorsFromAssembly</c>, so there is no registration to add.
/// </para>
/// </summary>
public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    /// <summary>The longest comment the <c>reviews.text</c> column holds.</summary>
    public const int TextMaximumLength = 2000;

    /// <summary>Declares the rules.</summary>
    public CreateReviewCommandValidator()
    {
        RuleFor(command => command.Rating)
            .InclusiveBetween(RatingScale.Minimum, RatingScale.Maximum)
                .WithMessage(nameof(ErrorCode.REVIEW_RATING_OUT_OF_RANGE));

        RuleFor(command => command.Text)
            .NotEmpty().WithMessage(nameof(ErrorCode.REVIEW_TEXT_REQUIRED))
            .MaximumLength(TextMaximumLength).WithMessage(nameof(ErrorCode.REVIEW_TEXT_TOO_LONG));
    }
}
