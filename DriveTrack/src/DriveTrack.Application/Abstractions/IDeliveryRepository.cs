using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions;

/// <summary>Persistence for <see cref="Delivery"/>. See <see cref="IClientRepository"/> for the shape rules (AD-6).</summary>
public interface IDeliveryRepository
{
    /// <summary>Loads a delivery, or null when there is none with that id.</summary>
    /// <remarks>
    /// Deliberately un-included, and it must stay that way. The delete path loads through here, and
    /// a tracked timeline entry makes EF client-cascade the dependent by issuing its own
    /// <c>DELETE</c> at trigger depth 1, where the append-only guard raises. With nothing tracked,
    /// the database's own cascade runs at depth 2, which the guard permits (AD-27).
    /// </remarks>
    Task<Delivery?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// One delivery, but only if <paramref name="scope"/> admits it — otherwise null, whether the
    /// row is missing or merely somebody else's (FR-26, FR-27).
    /// </summary>
    /// <remarks>
    /// A distinctly named method rather than an overload of <see cref="GetByIdAsync"/>, and the
    /// reason is the note above it: the delete path depends on that one being tracked and
    /// un-included, and an overload invites a caller to reach for whichever signature is nearest.
    /// This one is <c>AsNoTracking</c>, because every caller of it reads.
    /// <para>
    /// The narrowing is a <c>WHERE</c> rather than a check after the fact, for AD-3's reason and one
    /// more: a caller who is told "not found" learns nothing about a row they may not see, so the
    /// non-disclosing 404 the requirements ask for falls out of the query instead of having to be
    /// remembered at each call site.
    /// </para>
    /// </remarks>
    /// <param name="id">The delivery being addressed.</param>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Delivery?> FindVisibleAsync(int id, AccessScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// One page of deliveries narrowed to the rows the caller may see, filtered and sorted in SQL,
    /// with each row's two parties named and the size of the whole filtered set (FR-18, FR-20,
    /// FR-25, FR-101, NFR-27).
    /// </summary>
    /// <remarks>
    /// <para>
    /// With no column named the list is newest first - the row id descending, not
    /// <see cref="Delivery.CreatedAt"/>: the id comes from a sequence, so it is insertion order, and
    /// insertion order is creation order for every row this system writes. Every other order ends on
    /// the id as well, so OFFSET can never repeat or skip a row between two requests.
    /// </para>
    /// <para>
    /// There is no overload without <paramref name="scope"/>, and AD-3 is why: a page fetched
    /// unscoped and filtered afterwards answers an empty page for a driver whose rows fall outside
    /// the first page, and no amount of paging further finds them. The narrowing comes first, the
    /// filters are applied to what it leaves, and the total counts exactly that.
    /// </para>
    /// <para>
    /// The party names are read here, as part of the same query, because the driver and client
    /// filters and sorts are about them: a name that only arrives after the page has been cut cannot
    /// decide which rows are on it.
    /// </para>
    /// </remarks>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="criteria">The page, the order and the filters. The caller has already validated them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<DeliveryRow>> ListAsync(
        AccessScope scope,
        DeliveryListCriteria criteria,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every delivery the caller may see, newest first, unpaged. For the one picker that has to
    /// offer every row rather than a table's page: the review form's delivery choice.
    /// </summary>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Delivery>> ListVisibleAsync(AccessScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// The deliveries named by <paramref name="ids"/> that <paramref name="scope"/> admits, in one
    /// round trip.
    /// </summary>
    /// <remarks>
    /// Not a page and not ordered: the caller already holds the rows it is asking about — a page of
    /// reviews, each naming the delivery it is written on — and reads these to name their parties.
    /// It exists so that a capability needing N rows of somebody else's data asks once instead of N
    /// times; the mirror of <see cref="IReviewRepository.ListDriverRatingsAsync"/>, which the
    /// Drivers capability reads ratings through for the same reason.
    /// <para>
    /// The scope is a parameter for AD-3's reason, unchanged by the ids being explicit: a row the
    /// caller may not see must be absent from the answer rather than filtered out of it afterwards.
    /// An id the scope excludes is simply missing from the result, exactly as an id nobody ever
    /// wrote is.
    /// </para>
    /// <para>
    /// An empty <paramref name="ids"/> answers an empty list without asking the database; every
    /// caller reaching here with nothing to look up is a caller whose own page was empty.
    /// </para>
    /// </remarks>
    /// <param name="scope">The guard's answer to "whose rows may this caller see".</param>
    /// <param name="ids">The delivery ids being resolved. Duplicates are harmless.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Delivery>> ListByIdsAsync(
        AccessScope scope,
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken);

    /// <summary>
    /// The heaviest package among the driver's deliveries that have not been carried yet, or null
    /// when they have none — the one question FR-103's fleet-side arm asks.
    /// <para>
    /// Only <see cref="DeliveryStatus.Pending"/> and <see cref="DeliveryStatus.InTransit"/> count.
    /// A delivered or failed parcel has already been carried, and counting it would let a job
    /// finished last winter block a fleet change for good.
    /// </para>
    /// </summary>
    /// <param name="driverId">The driver whose load is being weighed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<decimal?> FindHeaviestActiveWeightForDriverAsync(
        DriverId driverId,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many deliveries hold each status, for the dashboard's open-deliveries tile. A status no
    /// delivery holds is absent rather than zero.
    /// </summary>
    Task<IReadOnlyList<DeliveryStatusCount>> CountByStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How many deliveries are overdue at <paramref name="now"/>, by
    /// <see cref="DriveTrack.Application.Deliveries.OverdueRule"/> - the same rule the delivery screens judge a row by.
    /// </summary>
    Task<int> CountOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>How many open deliveries - Pending or In transit - have no driver.</summary>
    Task<int> CountUnassignedOpenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The first <paramref name="limit"/> open deliveries whose window starts in
    /// [<paramref name="from"/>, <paramref name="until"/>), earliest window first.
    /// </summary>
    Task<IReadOnlyList<Delivery>> ListOpenStartingBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Stages a new delivery for the next commit.</summary>
    void Add(Delivery delivery);

    /// <summary>Stages a delivery for deletion (FR-24). Its timeline, proof, review and notification attempts go with it (DR-9).</summary>
    void Remove(Delivery delivery);
}

/// <summary>One status and how many deliveries currently hold it.</summary>
public sealed record DeliveryStatusCount(DeliveryStatus Status, int Count);

/// <summary>
/// What a delivery list is narrowed and ordered by, as the repository receives it: the query's
/// values parsed and its local days turned into instants.
/// </summary>
/// <param name="Offset">Rows to skip.</param>
/// <param name="Limit">Rows to take.</param>
/// <param name="Sort">The column, or null for newest first.</param>
/// <param name="Descending">True to reverse the order.</param>
/// <param name="Driver">Text the driver's name must contain, or null.</param>
/// <param name="Client">Text the client's name must contain, or null.</param>
/// <param name="Pickup">Text the pickup address must contain, or null.</param>
/// <param name="Dropoff">Text the dropoff address must contain, or null.</param>
/// <param name="PackageDetails">Text the package description must contain, or null.</param>
/// <param name="DeliveryNotes">Text the notes must contain, or null.</param>
/// <param name="Status">The only status to list, or null.</param>
/// <param name="CreatedFrom">The earliest creation instant to list, inclusive, or null.</param>
/// <param name="CreatedBefore">The creation instant every listed row is before, exclusive, or null.</param>
/// <param name="OverdueAt">
/// The instant to judge overdue at when only overdue deliveries are listed, or null to list every
/// delivery. The rule is <see cref="OverdueRule"/>'s, so the filter and the badge agree.
/// </param>
public sealed record DeliveryListCriteria(
    int Offset,
    int Limit,
    DeliverySort? Sort,
    bool Descending,
    string? Driver,
    string? Client,
    string? Pickup,
    string? Dropoff,
    string? PackageDetails,
    string? DeliveryNotes,
    DeliveryStatus? Status,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore,
    DateTimeOffset? OverdueAt);

/// <summary>A delivery and the names of its two parties, as one list row.</summary>
/// <param name="Delivery">The delivery.</param>
/// <param name="DriverName">The assigned driver's name, or null when there is none.</param>
/// <param name="ClientName">The requesting client's name, or null when there is none.</param>
public sealed record DeliveryRow(Delivery Delivery, string? DriverName, string? ClientName);
