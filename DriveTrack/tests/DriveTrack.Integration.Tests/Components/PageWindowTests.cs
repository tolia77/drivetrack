using DriveTrack.Web.Components.Shared;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The pager's page window: which page numbers <c>DtPager</c> draws, and how many pages a total
/// makes. Pure functions, asserted without rendering anything.
/// </summary>
public class PageWindowTests
{
    // =====================================================================================
    // The page window (design system, Pagination: "Pages to show")
    // =====================================================================================

    public static TheoryData<int, int, string> Windows() => new()
    {
        // Up to seven pages: all of them, no gap.
        { 1, 1, "1" },
        { 1, 7, "1 2 3 4 5 6 7" },
        { 4, 7, "1 2 3 4 5 6 7" },

        // Nine pages, at page 1 and at page 5.
        { 1, 9, "1 2 3 … 9" },
        { 5, 9, "1 … 4 5 6 … 9" },

        // Near either end, the three pages at that end.
        { 2, 9, "1 2 3 … 9" },
        { 3, 9, "1 2 3 4 … 9" },
        { 8, 9, "1 … 7 8 9" },
        { 9, 9, "1 … 7 8 9" },

        // Eight pages is the first count that gaps.
        { 4, 8, "1 … 3 4 5 … 8" },

        // A long list, and a current page out of range clamped back into it.
        { 50, 100, "1 … 49 50 51 … 100" },
        { 0, 9, "1 2 3 … 9" },
        { 42, 9, "1 … 7 8 9" },
    };

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_pager_draws_the_first_the_last_and_the_current_page_with_its_neighbours(
        int current,
        int pageCount,
        string expected)
    {
        var drawn = string.Join(
            " ",
            PageWindow.For(current, pageCount).Select(page => page?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "…"));

        Assert.Equal(expected, drawn);
    }

    [Fact]
    public void No_pages_draws_nothing_at_all()
    {
        // An empty list draws no bar, so the window it would have drawn is empty too.
        Assert.Empty(PageWindow.For(1, 0));
    }

    [Theory]
    [InlineData(86, 10, 9)]
    [InlineData(80, 10, 8)]
    [InlineData(1, 10, 1)]
    [InlineData(0, 10, 0)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    public void A_partial_last_page_is_still_a_page(int total, int size, int pages)
    {
        Assert.Equal(pages, PageWindow.Count(total, size));
    }
}
