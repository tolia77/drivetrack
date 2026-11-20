using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// The side-effect dispatch seam: what a delivery operation asks for once its own transaction has
/// committed, and never before.
/// <para>
/// Both members return <c>void</c> and take no <see cref="CancellationToken"/>, and both are
/// deliberate. Deriving an address must never block the user, and a failed
/// notification must never fail the write that caused it; a method returning <c>Task</c> is a
/// method a caller can await, and a comment asking them not to is not a guarantee. Making the
/// non-blocking promise part of the signature is what puts it beyond a hurried afternoon.
/// </para>
/// <para>
/// The absent token is the same argument from the other end. The request's token is cancelled the
/// moment the response is written, which is before the work here has started; handing it to a
/// background job would cancel every side effect the instant it became useful. A queued job runs
/// on the worker's own stopping token instead.
/// </para>
/// </summary>
public interface IDeliverySideEffects
{
    /// <summary>
    /// Asks for the delivery's missing addresses to be resolved from its coordinates.
    /// A point that already has one is left alone.
    /// </summary>
    /// <param name="deliveryId">The delivery whose points should be described.</param>
    void ResolveAddresses(int deliveryId);

    /// <summary>
    /// Asks for the delivery's client to be told that its status moved. A delivery with no
    /// client owes nobody anything and sends nothing.
    /// </summary>
    /// <param name="deliveryId">The delivery that moved.</param>
    /// <param name="previous">The status it held.</param>
    /// <param name="next">The status it holds now.</param>
    void NotifyStatusChanged(int deliveryId, DeliveryStatus previous, DeliveryStatus next);
}
