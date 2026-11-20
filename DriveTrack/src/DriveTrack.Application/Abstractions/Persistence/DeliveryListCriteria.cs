using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>
/// What a delivery list is narrowed and ordered by, as the repository receives it: the query's
/// values parsed and its local days turned into instants.
/// </summary>
/// <param name="Offset">Rows to skip.</param>
/// <param name="Limit">Rows to take.</param>
/// <param name="Sort">The column, or null for newest first.</param>
/// <param name="Descending">True to reverse the order.</param>
/// <param name="Driver">Text the driver's name must contain, or null.</param>
/// <param name="Client">Text the client's name must contain, or null.</param>
/// <param name="Pickup">Text the pickup address must contain, or null.</param>
/// <param name="Dropoff">Text the dropoff address must contain, or null.</param>
/// <param name="PackageDetails">Text the package description must contain, or null.</param>
/// <param name="DeliveryNotes">Text the notes must contain, or null.</param>
/// <param name="Status">The only status to list, or null.</param>
/// <param name="CreatedFrom">The earliest creation instant to list, inclusive, or null.</param>
/// <param name="CreatedBefore">The creation instant every listed row is before, exclusive, or null.</param>
/// <param name="OverdueAt">
/// The instant to judge overdue at when only overdue deliveries are listed, or null to list every
/// delivery. The rule is <see cref="OverdueRule"/>'s, so the filter and the badge agree.
/// </param>
public sealed record DeliveryListCriteria(
    int Offset,
    int Limit,
    DeliverySort? Sort,
    bool Descending,
    string? Driver,
    string? Client,
    string? Pickup,
    string? Dropoff,
    string? PackageDetails,
    string? DeliveryNotes,
    DeliveryStatus? Status,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore,
    DateTimeOffset? OverdueAt);
