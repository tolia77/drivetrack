using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Reviews;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// A review as dispatch and moderation read one: the row, the judgement it carries,
/// and both parties to the delivery it is about.
/// <para>
/// A distinct type from <see cref="AuthoredReviewSummary"/> rather than the same type with
/// the parties nulled out. The client who wrote a review must never learn which driver carried
/// their parcel, and the only way to make that impossible to regress by editing a mapping is for
/// the shape they receive to have no field a party could be written into.
/// </para>
/// </summary>
/// <param name="Id">The review row's id.</param>
/// <param name="DeliveryId">The delivery reviewed.</param>
/// <param name="Rating">The score, 1 to 5.</param>
/// <param name="Band">
/// How that score reads, decided by <see cref="RatingScale.Band"/> in Domain rather than by
/// a class name in markup.
/// </param>
/// <param name="Text">The written comment.</param>
/// <param name="CreatedAt">When it was written, at offset zero.</param>
/// <param name="Client">The client who wrote it, or null when the delivery names none.</param>
/// <param name="Driver">The driver it is about, or null when the delivery was never assigned.</param>
public sealed record ReviewSummary(
    int Id,
    int DeliveryId,
    int Rating,
    RatingBand Band,
    string Text,
    DateTimeOffset CreatedAt,
    DeliveryParty? Client,
    DeliveryParty? Driver);
