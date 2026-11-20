using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Shifts;
using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Shifts;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for <see cref="Shift"/>.
/// <para>
/// Open is <see cref="Shift.EndedAt"/> <c>IS NULL</c> everywhere in this port, and that is the same
/// predicate <c>IX_Shifts_DriverId_Open</c> filters on. Nothing time-dependent may
/// enter it and there is no status column to consult, so "is this driver on duty" is one comparison
/// asked in one way.
/// </para>
/// <para>
/// The repository rules hold here as everywhere: materialized results, no <c>IQueryable</c>, a
/// cancellation token last on every I/O method.
/// </para>
/// </summary>
public interface IShiftRepository
{
    /// <summary>Loads a shift, or null when there is none with that id.</summary>
    Task<Shift?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// The driver's open shift, or null when they are off duty.
    /// <para>
    /// The friendly answer's source, never the rule: two requests racing both find nothing here,
    /// and only the index refuses the second insert. A caller uses this to say
    /// <c>SHIFT_ALREADY_OPEN</c> before writing and lets the index be the backstop.
    /// </para>
    /// </summary>
    Task<Shift?> FindOpenAsync(DriverId driverId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any other shift of the driver overlaps the half-open window
    /// <c>[start, end ?? now)</c>. An open shift - the candidate or a stored one - occupies time up
    /// to <paramref name="now"/> and no further, so a window rostered ahead does not collide with
    /// the shift a driver is on today, and going on duty does not collide with tomorrow's roster.
    /// Touching windows - one ending exactly when the other starts - do not overlap.
    /// <para>
    /// A lookup, so like <see cref="FindOpenAsync"/> it is the friendly answer rather than a rule
    /// two racing requests cannot both pass.
    /// </para>
    /// </summary>
    /// <param name="driverId">The driver whose shifts are compared.</param>
    /// <param name="start">The window's start.</param>
    /// <param name="end">The window's end, or null for an open shift.</param>
    /// <param name="excludingId">The shift being edited, which is never compared with itself; null on a create.</param>
    /// <param name="now">The instant an open shift is taken to run until.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> AnyOverlappingAsync(
        DriverId driverId,
        DateTimeOffset start,
        DateTimeOffset? end,
        int? excludingId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of shifts with each one's driver named, searched and sorted in SQL, and the size of
    /// the whole narrowed set. Newest first when no column is named.
    /// <para>
    /// The name comes from the Identity account the driver row points at, joined here rather than
    /// read as a roster afterwards: the search and the sort are both about that name, and both have
    /// to run over every shift rather than over the page.
    /// </para>
    /// </summary>
    /// <param name="driverId">
    /// The driver to narrow to, or null for every driver. The narrowing is the guard's answer and
    /// reaches the <c>WHERE</c> clause rather than filtering a materialized page, so paging stays
    /// correct for a driver whose rows are not the first hundred.
    /// </param>
    /// <param name="query">The page, the search and the direction. Already validated.</param>
    /// <param name="sort">The column, already parsed; null for newest first.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<ShiftRow>> ListAsync(
        DriverId? driverId,
        ListShiftsQuery query,
        ShiftSort? sort,
        CancellationToken cancellationToken);

    /// <summary>
    /// The drivers who are on duty right now: every driver holding an open shift.
    /// <para>
    /// Ids and nothing else, which is what keeps the Shifts capability free of a dependency on the
    /// Drivers one — the roster asks this, and this asks the roster for nothing.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<DriverId>> ListOpenDriverIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// When the earliest still-open shift started, or null when nobody is on duty.
    /// </summary>
    Task<DateTimeOffset?> FindEarliestOpenStartAsync(CancellationToken cancellationToken);

    /// <summary>Stages a new shift for the next commit.</summary>
    void Add(Shift shift);

    /// <summary>Stages a shift for removal.</summary>
    void Remove(Shift shift);
}
