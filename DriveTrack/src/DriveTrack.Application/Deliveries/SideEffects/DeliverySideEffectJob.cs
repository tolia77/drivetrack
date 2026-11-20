namespace DriveTrack.Application.Deliveries.SideEffects;

/// <summary>
/// One queued side effect a delivery operation leaves behind, carrying the row id rather
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
