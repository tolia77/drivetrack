using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries.SideEffects;

/// <summary>Tell the client their delivery moved, and record the attempt.</summary>
/// <param name="DeliveryId">The delivery that moved.</param>
/// <param name="Previous">The status held before the move.</param>
/// <param name="Next">The status held after the move.</param>
public sealed record StatusChangedJob(int DeliveryId, DeliveryStatus Previous, DeliveryStatus Next)
    : DeliverySideEffectJob(DeliveryId);
