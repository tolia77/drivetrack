using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Notifications;
using DriveTrack.Application.Reviews;
using DriveTrack.Application.Shifts;
using DriveTrack.Application.Users;
using DriveTrack.Application.Vehicles;
using FluentValidation;

namespace DriveTrack.Application.Tests.Common;

/// <summary>
/// The paging rules every list shares: which page numbers a pager draws, which sort names a list
/// accepts, and that every list validator refuses the same things the same way.
/// <para>
/// Pure functions and validators, so asserted here without Docker. Whether the database then
/// orders, filters and counts the way the page says is the integration suite's question.
/// </para>
/// </summary>
public class PagingTests
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

        // The spec's own examples: nine pages at page 1, and at page 5.
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

    // =====================================================================================
    // Sort names
    // =====================================================================================

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Status", true)]
    [InlineData("status", true)]
    [InlineData(" createdAt ", true)]
    [InlineData("bogus", false)]
    [InlineData("3", false)]
    [InlineData("42", false)]
    [InlineData("Status,Window", false)]
    public void A_sort_is_a_column_name_and_never_a_number(string? sort, bool known)
    {
        // Enum.TryParse would accept "3" - the fourth member by declaration order - and "42", which
        // is no member at all. Neither is a column name a caller could have read anywhere.
        Assert.Equal(known, ListSort.IsKnown<DeliverySort>(sort));
    }

    [Fact]
    public void A_known_sort_parses_to_its_column_and_an_absent_one_to_none()
    {
        Assert.Equal(DeliverySort.Status, ListSort.Parse<DeliverySort>("status"));
        Assert.Null(ListSort.Parse<DeliverySort>(null));
        Assert.Null(ListSort.Parse<DeliverySort>("bogus"));
    }

    // =====================================================================================
    // Every list validator
    // =====================================================================================

    public static TheoryData<string> Lists() =>
    [
        nameof(ListDeliveriesQuery),
        nameof(ListVehiclesQuery),
        nameof(ListShiftsQuery),
        nameof(ListReviewsQuery),
        nameof(ListNotificationAttemptsQuery),
        nameof(ListDriversQuery),
        nameof(ListClientsQuery),
        nameof(ListDispatchersQuery),
    ];

    [Theory]
    [MemberData(nameof(Lists))]
    public void Every_list_refuses_an_unknown_sort_with_the_shared_code(string list)
    {
        // The I/O matrix's bad-input row: `?sort=bogus` is a 422 naming the sort, never a quiet
        // fallback to an order the caller did not ask for.
        var failure = Assert.Single(Validate(list, offset: 0, limit: 10, sort: "bogus").Errors);

        Assert.Equal("Sort", failure.PropertyName);
        Assert.Equal(nameof(ErrorCode.COMMON_SORT_INVALID), failure.ErrorMessage);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void Every_list_keeps_the_ceiling_of_a_hundred(string list)
    {
        // The validator ceiling stays where it was (NFR-27): a page of a hundred is the most a caller
        // may ask for, and a hundred and one is refused with the shared paging code.
        Assert.True(Validate(list, offset: 0, limit: 100, sort: null).IsValid);

        // One past the ceiling, not merely far past it: a ceiling anywhere from 101 to 499 would
        // pass a check made only at 500.
        foreach (var limit in new[] { 101, 500 })
        {
            var failure = Assert.Single(Validate(list, offset: 0, limit: limit, sort: null).Errors);

            Assert.Equal("Limit", failure.PropertyName);
            Assert.Equal(nameof(ErrorCode.COMMON_PAGING_LIMIT_INVALID), failure.ErrorMessage);
        }

        var offset = Assert.Single(Validate(list, offset: -1, limit: 10, sort: null).Errors);

        Assert.Equal(nameof(ErrorCode.COMMON_PAGING_OFFSET_INVALID), offset.ErrorMessage);
    }

    [Fact]
    public void A_status_the_system_does_not_have_is_refused_rather_than_ignored()
    {
        var failure = Assert.Single(new ListDeliveriesQueryValidator()
            .Validate(new ListDeliveriesQuery(0, 10, Status: (Domain.Deliveries.DeliveryStatus)42))
            .Errors);

        Assert.Equal(nameof(ErrorCode.DELIVERY_STATUS_UNKNOWN), failure.ErrorMessage);
    }

    private static FluentValidation.Results.ValidationResult Validate(
        string list,
        int offset,
        int limit,
        string? sort) => list switch
    {
        nameof(ListDeliveriesQuery) => Run(new ListDeliveriesQueryValidator(), new ListDeliveriesQuery(offset, limit, sort)),
        nameof(ListVehiclesQuery) => Run(new ListVehiclesQueryValidator(), new ListVehiclesQuery(offset, limit, sort)),
        nameof(ListShiftsQuery) => Run(new ListShiftsQueryValidator(), new ListShiftsQuery(offset, limit, null, sort)),
        nameof(ListReviewsQuery) => Run(new ListReviewsQueryValidator(), new ListReviewsQuery(offset, limit, sort)),
        nameof(ListNotificationAttemptsQuery) => Run(new ListNotificationAttemptsQueryValidator(), new ListNotificationAttemptsQuery(offset, limit, sort)),
        nameof(ListDriversQuery) => Run(new ListDriversQueryValidator(), new ListDriversQuery(offset, limit, sort)),
        nameof(ListClientsQuery) => Run(new ListClientsQueryValidator(), new ListClientsQuery(offset, limit, sort)),
        nameof(ListDispatchersQuery) => Run(new ListDispatchersQueryValidator(), new ListDispatchersQuery(offset, limit, sort)),
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    private static FluentValidation.Results.ValidationResult Run<T>(IValidator<T> validator, T query) =>
        validator.Validate(query);
}
