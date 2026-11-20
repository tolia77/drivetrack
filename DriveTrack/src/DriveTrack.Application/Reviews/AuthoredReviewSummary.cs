using DriveTrack.Domain.Reviews;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// A review as the person who wrote it reads one, and as a moderator gets it back after an
/// edit: everything <see cref="ReviewSummary"/> carries, minus both parties.
/// <para>
/// The distinct-DTO rule again, and this is the type it is actually about. There is no field here a driver's
/// name could be put in and no <c>driverId</c> to correlate on, so a client reading their own
/// reviews cannot learn who carried their parcel however the mapping is later edited.
/// </para>
/// </summary>
/// <param name="Id">The review row's id.</param>
/// <param name="DeliveryId">
/// The delivery reviewed — the caller's own, by construction: they could not have written this
/// otherwise. It is what lets their own list say which parcel each verdict is about.
/// </param>
/// <param name="Rating">The score, 1 to 5.</param>
/// <param name="Band">How that score reads.</param>
/// <param name="Text">The written comment.</param>
/// <param name="CreatedAt">When it was written, at offset zero.</param>
public sealed record AuthoredReviewSummary(
    int Id,
    int DeliveryId,
    int Rating,
    RatingBand Band,
    string Text,
    DateTimeOffset CreatedAt);
