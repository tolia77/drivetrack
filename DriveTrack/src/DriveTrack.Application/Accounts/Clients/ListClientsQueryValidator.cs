using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Application.Common.Paging;
using FluentValidation;

namespace DriveTrack.Application.Accounts.Clients;

/// <summary>Paging "validated rather than passed through unchecked", over the client roster.</summary>
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
