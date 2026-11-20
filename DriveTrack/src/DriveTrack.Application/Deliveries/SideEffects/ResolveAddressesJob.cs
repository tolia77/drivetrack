namespace DriveTrack.Application.Deliveries.SideEffects;

/// <summary>Fill in whichever of the delivery's two points has no address yet.</summary>
/// <param name="DeliveryId">The delivery whose points to resolve.</param>
public sealed record ResolveAddressesJob(int DeliveryId) : DeliverySideEffectJob(DeliveryId);
