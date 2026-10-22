using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="TimelineEntry"/> — append and read, and nothing else.
/// <para>
/// There is deliberately no update and no per-row delete member. AD-27 is enforced by the
/// absence of the method as much as by the trigger in the migration: a code path that does not
/// exist cannot be called by mistake, and a trigger that exists cannot be bypassed by code that
/// forgets. A correction is a new entry (FR-108).
/// </para>
/// </summary>
public interface ITimelineEntryRepository
{
    /// <summary>Stages a new entry for the next commit. Written inside the same transaction as the change it records (AD-27).</summary>
    void Add(TimelineEntry entry);

    /// <summary>
    /// The <paramref name="limit"/> newest entries across every delivery, newest first.
    /// </summary>
    Task<IReadOnlyList<TimelineEntry>> ListLatestAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The distinct deliveries moved to <paramref name="status"/> by an entry that occurred in
    /// [<paramref name="from"/>, <paramref name="until"/>).
    /// </summary>
    Task<IReadOnlyList<int>> ListDeliveryIdsMovedToAsync(
        DeliveryStatus status,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);

    /// <summary>Returns a delivery's entries oldest first (FR-108), materialized (AD-6).</summary>
    Task<IReadOnlyList<TimelineEntry>> ListForDeliveryAsync(
        int deliveryId,
        CancellationToken cancellationToken);
}
