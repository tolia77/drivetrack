namespace DriveTrack.Application.Abstractions;

/// <summary>
/// The per-operation persistence scope. It owns one short-lived database context and one
/// transaction, and exposes the repositories that write through them.
/// <para>
/// One operation, one commit. Disposing without committing rolls the transaction back, so a
/// multi-step write either lands whole or not at all. Nothing injects a context
/// directly — a scoped context on a Blazor Server circuit lives for hours, accumulates tracked
/// entities and throws on concurrent renders, which is the trap this shape exists to avoid.
/// </para>
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// User accounts. Identity writes go through this scope and this
    /// commit, so an account and its subtype row can never be half-created.
    /// </summary>
    IUserAccountRepository Users { get; }

    /// <summary>Clients.</summary>
    IClientRepository Clients { get; }

    /// <summary>Drivers.</summary>
    IDriverRepository Drivers { get; }

    /// <summary>Vehicles.</summary>
    IVehicleRepository Vehicles { get; }

    /// <summary>Deliveries.</summary>
    IDeliveryRepository Deliveries { get; }

    /// <summary>Driver shifts.</summary>
    IShiftRepository Shifts { get; }

    /// <summary>Reviews.</summary>
    IReviewRepository Reviews { get; }

    /// <summary>Proof of delivery.</summary>
    IProofOfDeliveryRepository ProofOfDeliveries { get; }

    /// <summary>Notification attempts.</summary>
    INotificationAttemptRepository NotificationAttempts { get; }

    /// <summary>The append-only delivery timeline.</summary>
    ITimelineEntryRepository TimelineEntries { get; }

    /// <summary>Chat messages.</summary>
    IMessageRepository Messages { get; }

    /// <summary>
    /// Saves every staged change and commits the transaction. Called once per operation; a
    /// second call on the same scope is a programming error.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
