namespace DriveTrack.Application.Common;

/// <summary>
/// Which page numbers a pager draws, and where it draws a gap between them.
/// <para>
/// The design system's rule, as a pure function so it can be asserted on its own: up to seven pages
/// are all shown; past that the first page, the last page and the current page with one on each side
/// are shown, widened to the three pages at an end when the current page is near it, and a gap marks
/// every place where numbers are skipped. Nine pages at page 1 is <c>1 2 3 … 9</c>; at page 5 it is
/// <c>1 … 4 5 6 … 9</c>.
/// </para>
/// </summary>
public static class PageWindow
{
    /// <summary>The most pages drawn without a gap.</summary>
    public const int AllShownUpTo = 7;

    /// <summary>
    /// The page numbers to draw, in order, with null standing for a gap.
    /// </summary>
    /// <param name="current">The page on screen, one-based. Clamped into range.</param>
    /// <param name="pageCount">How many pages there are. Zero or less draws nothing.</param>
    public static IReadOnlyList<int?> For(int current, int pageCount)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        if (pageCount <= AllShownUpTo)
        {
            return [.. Enumerable.Range(1, pageCount).Select(page => (int?)page)];
        }

        var page = Math.Clamp(current, 1, pageCount);

        var shown = new SortedSet<int> { 1, pageCount, page - 1, page, page + 1 };

        // Near either end the three pages at that end, so page 1 reads 1 2 3 … rather than 1 2 …,
        // and the last page reads … 7 8 9 rather than … 8 9.
        if (page <= 2)
        {
            shown.UnionWith([1, 2, 3]);
        }

        if (page >= pageCount - 1)
        {
            shown.UnionWith([pageCount - 2, pageCount - 1, pageCount]);
        }

        shown.RemoveWhere(candidate => candidate < 1 || candidate > pageCount);

        var pages = new List<int?>(shown.Count * 2);
        var previous = 0;

        foreach (var number in shown)
        {
            if (previous != 0 && number - previous > 1)
            {
                pages.Add(null);
            }

            pages.Add(number);
            previous = number;
        }

        return pages;
    }

    /// <summary>How many pages <paramref name="total"/> rows fill at <paramref name="pageSize"/> a page.</summary>
    public static int Count(int total, int pageSize) =>
        total <= 0 || pageSize <= 0 ? 0 : (total + pageSize - 1) / pageSize;
}
