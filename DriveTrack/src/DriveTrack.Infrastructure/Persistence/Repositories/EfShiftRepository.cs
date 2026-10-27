using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Common;
using DriveTrack.Application.Shifts;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Shifts;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IShiftRepository"/>. See <see cref="EfClientRepository"/> for the AD-6 rules every repository here follows.</summary>
internal sealed class EfShiftRepository(AppDbContext context) : IShiftRepository
{
    /// <inheritdoc />
    public Task<Shift?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.Shifts.FirstOrDefaultAsync(shift => shift.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Shift?> FindOpenAsync(DriverId driverId, CancellationToken cancellationToken) =>
        // Tracked, not AsNoTracking: ending a shift writes to the row this returns, so the change
        // tracker has to be holding it when the caller sets EndedAt.
        //
        // `EndedAt == null` is the whole definition of open and is the index's own filter, so the
        // query the database is asked is the one the index answers.
        context.Shifts.FirstOrDefaultAsync(
            shift => shift.DriverId == driverId && shift.EndedAt == null,
            cancellationToken);

    /// <inheritdoc />
    public async Task<Paged<ShiftRow>> ListAsync(
        DriverId? driverId,
        ListShiftsQuery query,
        ShiftSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var shifts = context.Shifts.AsNoTracking();

        // AD-3: the narrowing is a WHERE, applied before OFFSET and LIMIT. Filtering the
        // materialized page instead would answer an empty page for a driver whose shifts fall
        // outside the first hundred - the defect the parameter exists to make unrepresentable.
        if (driverId is { } driver)
        {
            shifts = shifts.Where(shift => shift.DriverId == driver);
        }

        // Open is `EndedAt IS NULL` and nothing else - the index's own filter (FR-117).
        if (query.OpenOnly)
        {
            shifts = shifts.Where(shift => shift.EndedAt == null);
        }

        // The driver's name is on their Identity account, two hops from the shift. Written as a
        // correlated subquery rather than a join so a shift whose account is somehow missing stays
        // in the list with an empty name instead of vanishing from it. The `(int)(object)` cast is
        // the one spelling of "the integer behind this typed id" EF translates - as a no-op
        // `::int` - because a value converter's `.Value` cannot be read inside a query (AD-22).
        var rows = shifts.Select(shift => new Row
        {
            Shift = shift,
            DriverName = context.Drivers
                .Where(candidate => candidate.Id == shift.DriverId)
                .Join(
                    context.Users,
                    candidate => (int)(object)candidate.UserId,
                    user => user.Id,
                    (candidate, user) => (user.FirstName + " " + user.LastName).Trim())
                .FirstOrDefault(),
        });

        if (ListQueries.Term(query.Search) is { } term)
        {
            var pattern = ListQueries.Contains(term);

            rows = rows.Where(row => EF.Functions.ILike(row.DriverName!, pattern, ListQueries.EscapeCharacter));
        }

        var descending = query.Descending;

        // Every order ends on the id: PostgreSQL is free to return rows in any order without an
        // ORDER BY, and two shifts sharing an instant would otherwise make a page that can repeat
        // and skip rows between requests. With no column named, newest first.
        var page = await rows.ToPageAsync(
            filtered => sort switch
            {
                ShiftSort.Driver => filtered.SortBy(row => row.DriverName ?? string.Empty, descending)
                    .ThenSortBy(row => row.Shift.Id, descending),
                ShiftSort.StartedAt => filtered.SortBy(row => row.Shift.StartedAt, descending)
                    .ThenSortBy(row => row.Shift.Id, descending),

                // A shift still running has no end yet, which is neither the earliest end nor the
                // latest: it stays at the far end whichever way the column points.
                ShiftSort.EndedAt => filtered.SortNullsLastBy(row => row.Shift.EndedAt, descending)
                    .ThenSortBy(row => row.Shift.Id, descending),
                _ => filtered.SortBy(row => row.Shift.StartedAt, !descending)
                    .ThenSortBy(row => row.Shift.Id, !descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);

        return page.Map(row => new ShiftRow(row.Shift, row.DriverName ?? string.Empty));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DriverId>> ListOpenDriverIdsAsync(
        CancellationToken cancellationToken) =>
        // One round trip for the whole roster rather than one per driver, which is the shape that
        // makes a forty driver roster forty queries. The index makes at most one open row per
        // driver, so Distinct is a statement of that rather than a deduplication anything needs.
        await context.Shifts
            .AsNoTracking()
            .Where(shift => shift.EndedAt == null)
            .Select(shift => shift.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<DateTimeOffset?> FindEarliestOpenStartAsync(CancellationToken cancellationToken) =>
        // Min over a nullable projection answers null for an empty set rather than throwing.
        context.Shifts
            .AsNoTracking()
            .Where(shift => shift.EndedAt == null)
            .Select(shift => (DateTimeOffset?)shift.StartedAt)
            .MinAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(Shift shift) => context.Shifts.Add(shift);

    /// <inheritdoc />
    public void Remove(Shift shift) => context.Shifts.Remove(shift);

    /// <summary>The shape the list query is filtered and sorted in: an initializer, so EF can see through it.</summary>
    private sealed class Row
    {
        public Shift Shift { get; init; } = null!;

        public string? DriverName { get; init; }
    }
}
