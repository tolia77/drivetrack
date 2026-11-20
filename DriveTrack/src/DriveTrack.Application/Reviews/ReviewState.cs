namespace DriveTrack.Application.Reviews;

/// <summary>
/// The state a review will hold once an edit is applied — the merged shape, and the only thing
/// <c>ReviewStateValidator</c> is ever handed.
/// </summary>
/// <param name="Rating">The score the row will carry.</param>
/// <param name="Text">The comment the row will carry, already trimmed by the merge that built this.</param>
public sealed record ReviewState(int Rating, string? Text);
