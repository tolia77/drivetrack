using DriveTrack.Application.Common;
using DriveTrack.Domain.Reviews;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// What an author or a moderator sends to change a review.
/// <para>
/// Both fields are <see cref="Optional{T}"/>, so an edit that only changes the rating leaves
/// the text alone rather than clearing it. Neither field is clearable — a review with no text and a
/// review with no rating are both rows the schema forbids — which is why the merged state below is
/// what the validator judges rather than this.
/// </para>
/// <para>
/// The delivery is deliberately absent. A review is keyed one-to-one on the delivery it is about;
/// moving one to another delivery is deleting it and writing another.
/// </para>
/// </summary>
/// <param name="Rating">The new score, or absent to leave it alone.</param>
/// <param name="Text">The new comment, or absent to leave it alone.</param>
public sealed record UpdateReviewCommand(Optional<int> Rating, Optional<string> Text)
{
    /// <summary>
    /// The command with every absent field filled in from the stored row: the state the review will
    /// hold afterwards.
    /// <para>
    /// Distinguishing absent from null makes this the thing the validator reads. Validating the payload instead would refuse
    /// an edit that only changes the rating, because it carries no text.
    /// </para>
    /// <para>
    /// The text is trimmed here, exactly as <see cref="CreateReviewCommand.Trimmed"/> trims a
    /// create's. The merged state is what the validator judges <em>and</em> what the service
    /// stores, so trimming anywhere later would let the two write paths judge the same content
    /// differently — a comment of exactly the column's width with a trailing newline accepted on
    /// create and refused on edit.
    /// </para>
    /// </summary>
    /// <param name="review">The stored row the edit will be applied to.</param>
    internal ReviewState MergedOnto(Review review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ReviewState(Rating.Or(review.Rating), Text.Or(review.Text)?.Trim());
    }
}
