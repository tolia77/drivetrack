using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Dashboard;

/// <summary>What one timeline entry recorded.</summary>
public enum DashboardActivityKind
{
    /// <summary>The entry that brought a delivery into being: nowhere to its first status.</summary>
    Created,

    /// <summary>A note and nothing else: neither status is set.</summary>
    Comment,

    /// <summary>A move from one status to another, with or without a note.</summary>
    StatusChange,
}

/// <summary>One entry of the latest activity across every delivery.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="ActorName">Who acted, as the entry recorded them.</param>
/// <param name="ActorRole">The role they acted in.</param>
/// <param name="Kind">What the entry recorded.</param>
/// <param name="PreviousStatus">Where the delivery was, or null.</param>
/// <param name="NewStatus">Where it went, or null.</param>
/// <param name="Note">What was said, or null.</param>
/// <param name="Dropoff">The delivery's drop-off, or null when the delivery no longer reads.</param>
/// <param name="OccurredAt">When it happened.</param>
public sealed record DashboardActivity(
    int Id,
    string ActorName,
    UserRole ActorRole,
    DashboardActivityKind Kind,
    DeliveryStatus? PreviousStatus,
    DeliveryStatus? NewStatus,
    string? Note,
    LocationView? Dropoff,
    DateTimeOffset OccurredAt);
