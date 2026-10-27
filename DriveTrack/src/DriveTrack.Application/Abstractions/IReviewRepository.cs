using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Reviews;
using DriveTrack.Domain.Identity;
using DriveTrack.Domain.Reviews;

namespace DriveTrack.Application.Abstractions;

/// <summary>Persistence for <see cref="Review"/>. See <see cref="IClientRepository"/> for the shape rules (AD-6).</summary>
public interface IReviewRepository
{
    /// <summary>Loads a review, or null when there is none with that id.</summary>
    Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// One page of reviews, newest first, narrowed to the rows the caller may see (FR-63, FR-64,
    /// NFR-27).
    /// </summary>
    /// <remarks>
    /// There is no overload without <paramref name="scope"/>, and AD-3 is why: a page fetched
    /// unscoped and filtered afterwards answers an empty page for a client whose reviews fall
    /// outside the first hundred, and no amount of paging further finds them. The narrowing has to
    /// be part of the query, so it is part of the signature.
    /// <para>
    /// "Newest first" is the row id descending, for the reason <see cref="IDeliveryRepository"/>
    /// gives: the id comes from a sequence, so it is insertion order, and insertion order is
    /// creation order for every row this system writes. Newest rather than oldest for the reason it
    /// gives too — nobody pages past the first page, so ascending put the reviews just written
    /// where no moderator would reach them.
    /// </para>
    /// </remarks>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="offset">Rows to skip. The caller has already validated it.</param>
    /// <param name="limit">Rows to take. The caller has already validated it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Review>> ListAsync(
        AccessScope scope,
        int offset,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of reviews narrowed to the rows the caller may see, sorted in SQL over all of them,
    /// with the size of the narrowed set (FR-63, FR-64, NFR-27). Newest first when no column is
    /// named, and every order ends on the id so OFFSET never repeats or skips a row.
    /// <para>
    /// The two party sorts read the names off the Identity accounts here - the author through the
    /// client row, the driver through the reviewed delivery - because a sort has to run over every
    /// review rather than over a page already cut. Which names a screen then shows is still the
    /// Deliveries capability's answer (AD-24).
    /// </para>
    /// </summary>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="query">The page and the direction. Already validated.</param>
    /// <param name="sort">The column, already parsed; null for newest first.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<Review>> ListPageAsync(
        AccessScope scope,
        ListReviewsQuery query,
        ReviewSort? sort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every review the caller may see, newest first, unpaged: the review form has to know every
    /// delivery an author has already reviewed, not the ones on the table's page.
    /// </summary>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Review>> ListAllAsync(AccessScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// The standing of each of the named drivers, derived from the reviews of the deliveries they
    /// carried (FR-98, DR-18).
    /// </summary>
    /// <remarks>
    /// One grouped query over every named driver rather than one query per driver: a roster of
    /// forty drivers costs one round trip, not forty. The aggregation happens in the database,
    /// because pulling every review of every driver back to average them in memory is the same
    /// answer at a cost that grows with the whole table.
    /// <para>
    /// A driver with no reviews is <em>absent</em> from the result rather than present with zeroes.
    /// Mapping absence to a zero would put an unrated driver below every rated one and read as a
    /// complaint nobody made — so the empty case is expressed by there being no row at all.
    /// </para>
    /// <para>
    /// <c>Review</c> carries no driver key: the driver is reached as
    /// <c>Review.DeliveryId → Delivery.DriverId</c>, which is why this is a join rather than a
    /// group over one table.
    /// </para>
    /// </remarks>
    /// <param name="driverIds">The drivers being asked about. An empty collection asks nothing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DriverRating>> ListDriverRatingsAsync(
        IReadOnlyCollection<DriverId> driverIds,
        CancellationToken cancellationToken);

    /// <summary>Stages a new review for the next commit.</summary>
    void Add(Review review);

    /// <summary>Stages a review for deletion (FR-66).</summary>
    void Remove(Review review);
}
