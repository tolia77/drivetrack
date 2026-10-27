using DriveTrack.Application.Common;
using DriveTrack.Application.Shifts;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Shifts;

namespace DriveTrack.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="Shift"/>.
/// <para>
/// Open is <see cref="Shift.EndedAt"/> <c>IS NULL</c> everywhere in this port, and that is the same
/// predicate <c>IX_Shifts_DriverId_Open</c> filters on (FR-110, FR-117). Nothing time-dependent may
/// enter it and there is no status column to consult, so "is this driver on duty" is one comparison
/// asked in one way.
/// </para>
/// <para>
/// AD-6's rules hold here as everywhere: materialized results, no <c>IQueryable</c>, a cancellation
/// token last on every I/O method.
/// </para>
/// </summary>
public interface IShiftRepository
{
    /// <summary>Loads a shift, or null when there is none with that id.</summary>
    Task<Shift?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// The driver's open shift, or null when they are off duty (FR-110).
    /// <para>
    /// The friendly answer's source, never the rule: two requests racing both find nothing here,
    /// and only the index refuses the second insert. A caller uses this to say
    /// <c>SHIFT_ALREADY_OPEN</c> before writing and lets the index be the backstop (AD-20).
    /// </para>
    /// </summary>
    Task<Shift?> FindOpenAsync(DriverId driverId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of shifts with each one's driver named, searched and sorted in SQL, and the size of
    /// the whole narrowed set (FR-112, FR-113). Newest first when no column is named.
    /// <para>
    /// The name comes from the Identity account the driver row points at, joined here rather than
    /// read as a roster afterwards: the search and the sort are both about that name, and both have
    /// to run over every shift rather than over the page.
    /// </para>
    /// </summary>
    /// <param name="driverId">
    /// The driver to narrow to, or null for every driver. The narrowing is the guard's answer and
    /// reaches the <c>WHERE</c> clause rather than filtering a materialized page, so paging stays
    /// correct for a driver whose rows are not the first hundred (AD-3).
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
    /// The drivers who are on duty right now (FR-116): every driver holding an open shift.
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

    /// <summary>Stages a shift for removal (FR-114).</summary>
    void Remove(Shift shift);
}

/// <summary>A shift and the name of the driver it belongs to, as one list row.</summary>
/// <param name="Shift">The shift.</param>
/// <param name="DriverName">The driver's first and last name, or empty when the account is missing.</param>
public sealed record ShiftRow(Shift Shift, string DriverName);
