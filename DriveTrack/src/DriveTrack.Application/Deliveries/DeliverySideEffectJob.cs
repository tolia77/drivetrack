using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// One queued side effect a delivery operation leaves behind (AD-12), carrying the row id rather
/// than the entity.
/// <para>
/// An id and not a <c>Delivery</c>: the entity belongs to the scope that loaded it, and that scope
/// is disposed by the time the job runs. The job reloads through its own unit of work, which is
/// also what makes it correct for a delivery changed twice in quick succession — the second run
/// sees the second state rather than a snapshot of the first.
/// </para>
/// </summary>
/// <param name="DeliveryId">The delivery the work is about.</param>
public abstract record DeliverySideEffectJob(int DeliveryId);

/// <summary>Fill in whichever of the delivery's two points has no address yet (FR-94).</summary>
/// <param name="DeliveryId">The delivery whose points to resolve.</param>
public sealed record ResolveAddressesJob(int DeliveryId) : DeliverySideEffectJob(DeliveryId);

/// <summary>Tell the client their delivery moved, and record the attempt (FR-28).</summary>
/// <param name="DeliveryId">The delivery that moved.</param>
/// <param name="Previous">The status held before the move.</param>
/// <param name="Next">The status held after the move.</param>
public sealed record StatusChangedJob(int DeliveryId, DeliveryStatus Previous, DeliveryStatus Next)
    : DeliverySideEffectJob(DeliveryId);
