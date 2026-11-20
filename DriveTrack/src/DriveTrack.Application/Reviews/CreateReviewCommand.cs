namespace DriveTrack.Application.Reviews;

/// <summary>
/// What a client sends to write a review.
/// <para>
/// The author is not a field, and never can be: it is <c>IAccessGuard.RequireReviewAuthor</c>'s
/// answer. An author the caller could name is an author the caller could forge.
/// </para>
/// </summary>
/// <param name="DeliveryId">The delivery being reviewed. One review per delivery.</param>
/// <param name="Rating">The score, 1 to 5.</param>
/// <param name="Text">
/// The written comment. Nullable because a caller can leave a box empty, and an empty box is a
/// refusal to state rather than a value to guess at — the validator turns it into a 422 naming the
/// field.
/// </param>
public sealed record CreateReviewCommand(int DeliveryId, int Rating, string? Text)
{
    /// <summary>
    /// The command as it will actually be stored: the comment with the caller's stray whitespace
    /// taken off.
    /// <para>
    /// The validator has to judge this rather than the payload, or a comment of exactly the
    /// column's width with a trailing newline is refused for being one character too long when the
    /// string that reaches the column fits. It is a method here rather than a line in the service
    /// so that the edit path can apply the same one — the two paths judging the same content differently is the defect this exists to prevent.
    /// </para>
    /// </summary>
    internal CreateReviewCommand Trimmed() => this with { Text = Text?.Trim() };
}
