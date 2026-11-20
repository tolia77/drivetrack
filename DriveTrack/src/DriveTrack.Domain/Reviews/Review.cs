
using DriveTrack.Domain.Clients;

namespace DriveTrack.Domain.Reviews;

/// <summary>
/// A client's verdict on one completed delivery.
/// <para>
/// Exactly one review may exist per delivery, and the rating must be 1 to 5. Both are database
/// constraints, not application checks: the original enforced one-review-per-delivery with an
/// <c>if</c>, which two concurrent requests defeat.
/// </para>
/// </summary>
public sealed class Review
{
    /// <summary>Surrogate key.</summary>
    public int Id { get; set; }

    /// <summary>The delivery reviewed. Unique across the table; the review dies with the delivery.</summary>
    public int DeliveryId { get; set; }

    /// <summary>The client who wrote it. The review dies with the client.</summary>
    public ClientId ClientId { get; set; }

    /// <summary>Score from 1 to 5, enforced by a check constraint.</summary>
    public int Rating { get; set; }

    /// <summary>The written comment.</summary>
    public required string Text { get; set; }

    /// <summary>When it was written, at offset zero.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
