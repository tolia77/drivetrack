using DriveTrack.Application.Accounts.Clients;
using DriveTrack.Application.Accounts.Dispatchers;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Deliveries;
using DriveTrack.Application.Drivers;
using DriveTrack.Application.Notifications;
using DriveTrack.Application.Reviews;
using DriveTrack.Application.Shifts;
using DriveTrack.Application.Vehicles;
using FluentValidation;

namespace DriveTrack.Application.Tests.Common;

/// <summary>
/// The paging rules every list shares: which sort names a list accepts, and that every list
/// validator refuses the same things the same way.
/// <para>
/// Pure functions and validators, so asserted here without Docker. Whether the database then
/// orders, filters and counts the way the page says is the integration suite's question.
/// </para>
/// </summary>
public class PagingTests
{
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
        // The bad-input case: `?sort=bogus` is a 422 naming the sort, never a quiet
        // fallback to an order the caller did not ask for.
        var failure = Assert.Single(Validate(list, offset: 0, limit: 10, sort: "bogus").Errors);

        Assert.Equal("Sort", failure.PropertyName);
        Assert.Equal(nameof(ErrorCode.COMMON_SORT_INVALID), failure.ErrorMessage);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void Every_list_keeps_the_ceiling_of_a_hundred(string list)
    {
        // The validator ceiling stays where it was: a page of a hundred is the most a caller
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
