using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Reviews;

/// <summary>
/// One driver's standing, derived on demand from the reviews of the deliveries they carried.
/// <para>
/// Never a column on <c>drivers</c>. A stored average is a second answer that goes stale the moment
/// a review is written, edited or deleted, and the original system's named defect is precisely that
/// ratings existed per review and were aggregated nowhere — a stored copy would have been the same
/// defect with a cache in front of it.
/// </para>
/// <para>
/// A driver with no reviews has no row here at all, rather than a row of zeroes. Absence is what
/// the roster renders its "no value" text for; a zero would sort an unrated driver below every
/// rated one and read as a complaint nobody made.
/// </para>
/// </summary>
/// <param name="DriverId">The driver row the reviews were reached through.</param>
/// <param name="Average">The mean of every rating, unrounded.</param>
/// <param name="ReviewCount">How many reviews it was drawn from.</param>
public sealed record DriverRating(DriverId DriverId, double Average, int ReviewCount);
