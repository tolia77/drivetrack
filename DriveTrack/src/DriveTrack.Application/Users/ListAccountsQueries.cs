using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Users;

/// <summary>
/// NFR-27's paging over the client roster, with the roster screen's sort, applied in SQL over every
/// client before OFFSET and LIMIT.
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListClientsQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="ClientSort"/> member's name, or null for surname order. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
public sealed record ListClientsQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false);

/// <summary>The columns the client roster can be ordered by.</summary>
public enum ClientSort
{
    /// <summary>Given name.</summary>
    FirstName,

    /// <summary>Family name, then given name.</summary>
    LastName,

    /// <summary>Sign-in address.</summary>
    Email,

    /// <summary>Contact number.</summary>
    PhoneNumber,
}

/// <summary>NFR-27's "validated rather than passed through unchecked", over the client roster.</summary>
public sealed class ListClientsQueryValidator : AbstractValidator<ListClientsQuery>
{
    /// <summary>The largest page a caller may ask for, which is also the REST default page size.</summary>
    public const int MaximumLimit = 100;

    /// <summary>Declares the rules.</summary>
    public ListClientsQueryValidator()
    {
        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0).WithMessage(nameof(ErrorCode.COMMON_PAGING_OFFSET_INVALID));

        RuleFor(query => query.Limit)
            .GreaterThan(0).WithMessage(nameof(ErrorCode.COMMON_PAGING_LIMIT_INVALID))
            .LessThanOrEqualTo(MaximumLimit)
                .WithMessage(nameof(ErrorCode.COMMON_PAGING_LIMIT_INVALID));

        RuleFor(query => query.Sort)
            .Must(ListSort.IsKnown<ClientSort>).WithMessage(nameof(ErrorCode.COMMON_SORT_INVALID));
    }
}

/// <summary>
/// NFR-27's paging over the dispatcher roster, with the roster screen's sort, applied in SQL over
/// every dispatcher before OFFSET and LIMIT.
/// </summary>
/// <param name="Offset">Rows to skip. Zero is the first page.</param>
/// <param name="Limit">Rows to take, at most <see cref="ListDispatchersQueryValidator.MaximumLimit"/>.</param>
/// <param name="Sort">
/// A <see cref="DispatcherSort"/> member's name, or null for surname order. Any other text is refused
/// with <c>COMMON_SORT_INVALID</c>.
/// </param>
/// <param name="Descending">True to reverse the order. The id tie-break follows it.</param>
public sealed record ListDispatchersQuery(
    int Offset,
    int Limit,
    string? Sort = null,
    bool Descending = false);

/// <summary>The columns the dispatcher roster can be ordered by.</summary>
public enum DispatcherSort
{
    /// <summary>Given name.</summary>
    FirstName,

    /// <summary>Family name, then given name.</summary>
    LastName,

    /// <summary>Sign-in address.</summary>
    Email,
}

/// <summary>NFR-27's "validated rather than passed through unchecked", over the dispatcher roster.</summary>
public sealed class ListDispatchersQueryValidator : AbstractValidator<ListDispatchersQuery>
{
    /// <summary>The largest page a caller may ask for, which is also the REST default page size.</summary>
    public const int MaximumLimit = 100;

    /// <summary>Declares the rules.</summary>
    public ListDispatchersQueryValidator()
    {
        RuleFor(query => query.Offset)
            .GreaterThanOrEqualTo(0).WithMessage(nameof(ErrorCode.COMMON_PAGING_OFFSET_INVALID));

        RuleFor(query => query.Limit)
            .GreaterThan(0).WithMessage(nameof(ErrorCode.COMMON_PAGING_LIMIT_INVALID))
            .LessThanOrEqualTo(MaximumLimit)
                .WithMessage(nameof(ErrorCode.COMMON_PAGING_LIMIT_INVALID));

        RuleFor(query => query.Sort)
            .Must(ListSort.IsKnown<DispatcherSort>).WithMessage(nameof(ErrorCode.COMMON_SORT_INVALID));
    }
}
