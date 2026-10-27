using DriveTrack.Application.Common;

namespace DriveTrack.Integration.Tests.Support;

/// <summary>
/// A stub service's page: the slice of a fixed row list that the query's offset and limit ask for,
/// with the whole list's count as the total - which is what a real list service answers.
/// </summary>
internal static class TestPage
{
    /// <summary>The page of <paramref name="rows"/> at <paramref name="offset"/>.</summary>
    public static Paged<T> Of<T>(IReadOnlyList<T> rows, int offset, int limit)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return new Paged<T>([.. rows.Skip(offset).Take(limit)], rows.Count, offset, limit);
    }

    /// <summary>Every row on one page, which is what a stub with a handful of rows usually means.</summary>
    public static Paged<T> All<T>(IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return new Paged<T>(rows, rows.Count, 0, Math.Max(rows.Count, 1));
    }
}
