namespace DriveTrack.Application.Shifts;

/// <summary>
/// The state a shift will hold once an edit is applied — the merged shape, and the only thing
/// <see cref="ShiftStateValidator"/> is ever handed.
/// </summary>
/// <param name="StartedAt">The start the row will carry.</param>
/// <param name="EndedAt">The end the row will carry, or null while it is open.</param>
public sealed record ShiftState(DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
