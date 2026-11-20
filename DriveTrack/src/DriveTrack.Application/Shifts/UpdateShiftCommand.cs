using DriveTrack.Application.Common;
using DriveTrack.Domain.Shifts;

namespace DriveTrack.Application.Shifts;

/// <summary>
/// What a caller sends to correct a shift.
/// <para>
/// Both fields are <see cref="Optional{T}"/>, so an edit that only moves the start leaves
/// the end alone rather than clearing it.
/// </para>
/// <para>
/// <see cref="EndedAt"/> is <c>Optional&lt;DateTimeOffset&gt;</c> and deliberately not
/// <c>Optional&lt;DateTimeOffset?&gt;</c>, and that one character is what keeps a closed shift closed. Present always means a
/// real instant and absent always means "leave the row's value": there is no third case, so
/// reopening a closed shift is not something this contract can express. The wire agrees — the
/// converter refuses a <c>null</c> for a field that cannot hold one — so
/// <c>{"endedAt": null}</c> is a 422 rather than a reopened shift.
/// </para>
/// <para>
/// The driver is deliberately absent. A shift is one driver's stretch of time; moving it to another
/// driver is deleting it and writing another, and a movable driver would let one edit slip a row
/// past the open-shift index by carrying it to a driver who already has one.
/// </para>
/// </summary>
/// <param name="StartedAt">The new start, or absent to leave it alone.</param>
/// <param name="EndedAt">The new end, or absent to leave it alone. Never a clear.</param>
public sealed record UpdateShiftCommand(
    Optional<DateTimeOffset> StartedAt,
    Optional<DateTimeOffset> EndedAt)
{
    /// <summary>
    /// The command with every absent field filled in from the stored row: the state the shift will
    /// hold afterwards.
    /// <para>
    /// Distinguishing absent from null makes this the thing the validator reads. Validating the payload instead would accept
    /// <c>{"startedAt": T+9h}</c> against a shift that ended at <c>T+8h</c> — a window that runs
    /// backwards, assembled out of two individually harmless halves.
    /// </para>
    /// </summary>
    /// <param name="shift">The stored row the edit will be applied to.</param>
    internal ShiftState MergedOnto(Shift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);

        // EndedAt is Optional<DateTimeOffset>, so `HasValue` means a real instant and absence means
        // the row's own value. Written out rather than as `EndedAt.Or(...)` because the types
        // differ: the command carries a non-nullable instant and the row carries a nullable one.
        return new ShiftState(
            StartedAt.Or(shift.StartedAt),
            EndedAt.HasValue ? EndedAt.Value : shift.EndedAt);
    }
}
