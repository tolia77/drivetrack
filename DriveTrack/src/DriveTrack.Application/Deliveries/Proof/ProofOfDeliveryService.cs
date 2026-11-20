using System.Globalization;
using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Domain.Common;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;
using FluentValidation;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// Proof of delivery: capturing the evidence that a parcel changed hands, and reading it back.
/// <para>
/// The one-guard rule applies here exactly as it does to <see cref="DeliveryService"/>: every
/// public method calls <see cref="IAccessGuard"/> through the interface, inline in its own body,
/// because the coverage gate walks each method's IL and does not follow a call it makes.
/// </para>
/// <para>
/// <b>Why <see cref="CaptureAsync"/> opens two scopes.</b> The rule that no store call happens
/// with a unit of work open still holds; what moved is which side of the commit the writing happens
/// on. The bytes go in <em>after</em> the row that names them is committed, because the alternative —
/// writing first — leaves an object no row will ever name behind every refusal the later steps can
/// raise, and <c>IAssetStore</c> has no delete to take it back with. Minting a key creates nothing,
/// so the keys can be named up front and the bucket stays untouched until the proof is safe.
/// </para>
/// <para>
/// That still needs two scopes. Authorizing first needs the delivery row, and the only way to read a
/// row here is a unit of work, which begins a transaction. So the capture reads, authorizes and
/// validates in a scope that never commits (the established read pattern), disposes it, mints the
/// keys, opens the scope that commits, and writes the bytes last with nothing open. The alternative
/// ordering — store first, guard second — is one method shorter and lets any signed-in caller push
/// bytes into the bucket before being refused.
/// </para>
/// <para>
/// When the store refuses a write after the commit, the proof is removed again in a fresh scope and
/// the failure propagates. Keeping it would leave evidence that does not resolve, which
/// immutability then makes permanent (a second capture is refused) and the proof-required gate
/// would accept as proof for Delivered.
/// Removing it lets the driver capture again. Any bytes that did land before the failure stay in
/// the bucket unreferenced, because <see cref="IAssetStore"/> has no delete.
/// </para>
/// </summary>
public sealed class ProofOfDeliveryService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IAccessGuard accessGuard,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IAssetStore assetStore,
    IValidator<CaptureProofCommand> captureValidator) : IProofOfDeliveryService
{
    /// <inheritdoc />
    public async Task<ProofOfDeliveryView> CaptureAsync(
        int deliveryId,
        CaptureProofCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Step one: read, authorize and validate in a scope that commits nothing. The block is
        // explicit rather than an `await using var` declaration because where this scope ends is
        // still the whole point - the store write below happens with no transaction open.
        await using (var reading = await unitOfWorkFactory.CreateAsync(cancellationToken))
        {
            var delivery = await reading.Deliveries.GetByIdAsync(deliveryId, cancellationToken);

            // The same predicate the status change uses: the driver carrying the parcel, or
            // dispatch, and never the client whose delivery it is. Run on a null row deliberately,
            // so a caller who may not act on the delivery is refused before learning it exists.
            accessGuard.RequireAssignedDriver(delivery?.DriverId);

            if (delivery is null)
            {
                throw NotFound(deliveryId);
            }

            // Upload validation, and here rather than later: a caller whose upload is refused costs
            // the store no call at all - not even a minted key - so the whole set is judged while
            // the cheapest thing to do about it is nothing.
            await captureValidator.EnsureValidAsync(command, cancellationToken);

            // A proof is immutable, so the second capture is refused rather than merged.
            // Asked here so the refusal costs no round trip to the store; asked again inside the
            // writing transaction, below, because two captures racing would both pass this check.
            if (await reading.ProofOfDeliveries.ExistsForDeliveryAsync(deliveryId, cancellationToken))
            {
                throw AlreadyCaptured(deliveryId);
            }

            // No commit: nothing was written, and the empty transaction rolls back on dispose. That
            // is the ordinary read path here as it is everywhere else.
        }

        // Step two: the keys, and only the keys. Minting names an object; it does not create one, so
        // every refusal below - the re-check, the unique index, a failed commit - still leaves the
        // bucket exactly as it was found. This is the whole reason NewKey exists.
        var pending = new List<PendingAsset>(command.Assets!.Count);

        foreach (var upload in command.Assets)
        {
            // Normalised once, here, and lower-cased as well as trimmed. A MIME type is
            // case-insensitive by its own specification, so ProofAssetRules accepts IMAGE/PNG - but
            // what a caller shouted is not what the system should then keep repeating. Stored
            // verbatim it would be the column's value, the proof view's value and the asset
            // route's Content-Type header, and two rows would hold different spellings of one type.
            // This is the single site every one of those reads from.
            var contentType = upload.ContentType!.Trim().ToLowerInvariant();

            pending.Add(new PendingAsset(
                upload.Kind,
                contentType,
                assetStore.NewKey(contentType),
                upload.Content));
        }

        // Step three: the transaction that names the keys, opened while the bucket is still empty of
        // this capture. A block rather than a declaration, for the reason the reading scope is one:
        // the store writes below must happen with no unit of work open, and a declaration
        // would hold this one - and the pooled connection under it - for the whole time the bytes
        // are travelling.
        ProofOfDelivery proof;
        string? capturedBy;

        await using (var writing = await unitOfWorkFactory.CreateAsync(cancellationToken))
        {
            // Re-asked inside the transaction. Two captures a second apart both pass the check above
            // and both reach here; this one loses, and the unique index on delivery_id is the
            // backstop behind it should they arrive closer than that - which answers the same code,
            // because PostgresConstraintTranslator maps ix_proof_of_deliveries_delivery_id onto it.
            if (await writing.ProofOfDeliveries.ExistsForDeliveryAsync(deliveryId, cancellationToken))
            {
                throw AlreadyCaptured(deliveryId);
            }

            proof = new ProofOfDelivery
            {
                DeliveryId = deliveryId,
                RecipientName = command.RecipientName!.Trim(),

                // No address and no resolution timestamp: the coordinates are the record, and the
                // address is a cache nothing has filled in yet.
                CaptureLocation = new Location(
                    command.CaptureLocation!.Latitude!.Value,
                    command.CaptureLocation.Longitude!.Value,
                    Address: null,
                    AddressResolvedAt: null),

                // The injected clock, at offset zero. A capture instant the caller could name is
                // an instant a caller could choose, which is the one thing evidence must not permit.
                CapturedAt = timeProvider.GetUtcNow(),
                CapturedByUserId = currentUser.UserId,
            };

            foreach (var asset in pending)
            {
                proof.Assets.Add(new ProofAsset
                {
                    Kind = asset.Kind,
                    StorageKey = asset.Key,
                    ContentType = asset.ContentType,
                });
            }

            writing.ProofOfDeliveries.Add(proof);

            // Read before the commit, inside the transaction that is about to close, as the delivery
            // capability reads its parties: a query issued after CommitAsync would run outside the
            // scope this operation was given.
            capturedBy = await CapturerNameAsync(writing, currentUser.UserId, cancellationToken);

            await writing.CommitAsync(cancellationToken);
        }

        // Step four: the bytes, with the row already safe and every scope closed. Both surfaces hold
        // their streams open for the whole call - the REST adapter reads over the buffered request
        // body, the capture panel disposes its browser files in a finally - so reading them here is
        // as valid as reading them before the commit was. Not equally robust, though: the REST
        // body is already buffered, while a browser file is still a read over a live circuit, which
        // this ordering now holds open across the commit. The proof is read after its scope closes
        // as well, which is safe because nothing on it is lazily loaded: every asset on it was put
        // there above.
        //
        // A failure removes the proof again (see RemoveUnstoredAsync) and then propagates, so the
        // request still fails and the driver can capture again.
        //
        // CancellationToken.None, and the only place in this capability that refuses the caller's
        // token. Everything above it is cancellable because cancelling it abandons a request that
        // changed nothing; this loop is not, because the row is already committed and the request is
        // no longer what the work is about. A driver who closes the dialog - or a client that hangs
        // up - would otherwise cancel between CommitAsync and the first upload and leave a proof
        // whose evidence was never written, with nobody left to be told. It holds for a closed
        // dialog and a hung-up client, which cancel a token while the connection carrying the bytes
        // stays up; it cannot hold for a circuit that dies outright, because the panel's photographs
        // are read over that circuit and no token choice makes a dead one readable.
        try
        {
            foreach (var asset in pending)
            {
                await assetStore.SaveAsync(asset.Key, asset.Content, asset.ContentType, CancellationToken.None);
            }
        }
        catch (Exception failure)
        {
            await RemoveUnstoredAsync(proof.Id, failure);
            throw;
        }

        return View(proof, capturedBy);
    }

    /// <summary>
    /// Takes back a proof whose evidence did not reach the store, so immutability does not lock the
    /// delivery to evidence that does not resolve. The asset rows go with it by cascade.
    /// <para>
    /// <see cref="CancellationToken.None"/> for the reason the upload loop uses it: the request is
    /// already failing, and abandoning this step would leave the very row it exists to remove. If
    /// the removal itself fails, both failures are reported together rather than one hiding the other.
    /// </para>
    /// </summary>
    private async Task RemoveUnstoredAsync(int proofId, Exception failure)
    {
        try
        {
            await using var cleanup = await unitOfWorkFactory.CreateAsync(CancellationToken.None);

            if (await cleanup.ProofOfDeliveries.GetByIdAsync(proofId, CancellationToken.None) is { } stale)
            {
                cleanup.ProofOfDeliveries.Remove(stale);
                await cleanup.CommitAsync(CancellationToken.None);
            }
        }
        catch (Exception removal)
        {
            throw new AggregateException(
                "A proof whose evidence was not stored could not be removed again.",
                failure,
                removal);
        }
    }

    /// <inheritdoc />
    public async Task<ProofOfDeliveryView> GetAsync(int deliveryId, CancellationToken cancellationToken)
    {
        // The proof's audience is "anyone who can view the delivery", which is what the scope
        // answers - including the client, who may read the evidence and may never capture it.
        var scope = accessGuard.RequireScope();

        await using var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken);

        // Narrowed in the query, so a client asking about another client's proof gets the same
        // answer as one asking about a delivery that never existed.
        var proof = await unitOfWork.ProofOfDeliveries.FindVisibleByDeliveryAsync(
                        deliveryId,
                        scope,
                        cancellationToken)
                    ?? throw NotFound(deliveryId);

        return View(proof, await CapturerNameAsync(unitOfWork, proof.CapturedByUserId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ProofAssetContent> OpenAssetAsync(int assetId, CancellationToken cancellationToken)
    {
        var scope = accessGuard.RequireScope();

        string key;
        string contentType;

        // The scope is a block rather than a declaration for the reason the capture's first scope
        // is: the store read below happens with nothing open, and a `await using var` here
        // would hold the transaction open for the whole time the bytes are being fetched.
        await using (var unitOfWork = await unitOfWorkFactory.CreateAsync(cancellationToken))
        {
            var asset = await unitOfWork.ProofOfDeliveries.FindVisibleAssetAsync(
                            assetId,
                            scope,
                            cancellationToken)
                        ?? throw NoSuchAsset(assetId);

            key = asset.StorageKey;
            contentType = asset.ContentType;
        }

        // A row naming an object the store no longer holds is a 404 and not a 500: the bucket can be
        // emptied out of band, and the honest answer to "show me this image" is then that there is
        // none. Non-disclosing, and the same answer a caller with no business here already gets.
        var content = await assetStore.OpenAsync(key, cancellationToken)
                      ?? throw NoSuchAsset(assetId);

        return new ProofAssetContent(contentType, content);
    }

    /// <summary>
    /// One asset on its way to the store: what it is, the key it has been promised, and the bytes
    /// that are not there yet.
    /// <para>
    /// Named for the gap it spans. Between minting and writing the key exists and the object does
    /// not, and this record is what carries the pair across the transaction that commits the first
    /// half — which is why it holds the caller's stream rather than a key it has already earned.
    /// </para>
    /// </summary>
    /// <param name="Kind">Signature or photograph.</param>
    /// <param name="ContentType">The normalised MIME type, stored on the row and with the object.</param>
    /// <param name="Key">The key the store minted, committed before anything is written under it.</param>
    /// <param name="Content">The caller's stream, read once the row is safe.</param>
    private sealed record PendingAsset(
        ProofAssetKind Kind,
        string ContentType,
        string Key,
        Stream Content);

    /// <summary>
    /// The disclosure decision, taken here and nowhere else: dispatch is told who captured the
    /// proof, and everyone else is told that it was captured.
    /// <para>
    /// The lookup is skipped entirely for a caller who may not be told, so a client's read costs no
    /// account query at all — the answer was null before the name was ever fetched.
    /// </para>
    /// </summary>
    /// <param name="unitOfWork">The open scope to read the account through.</param>
    /// <param name="capturedByUserId">Who captured it, or null once that account is deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The display name, or null when it is withheld, unknown or the account is gone.</returns>
    private async Task<string?> CapturerNameAsync(
        IUnitOfWork unitOfWork,
        UserId? capturedByUserId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Dispatcher))
        {
            return null;
        }

        if (capturedByUserId is not { } userId)
        {
            return null;
        }

        var account = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        // A deleted capturer leaves the set-null the schema declares, and a proof whose capturer's
        // row is gone is still evidence. Null here reads the same as withheld, which is the only
        // reading this shape supports and the correct one either way: there is no name to show.
        return account?.DisplayName;
    }

    private static ProofOfDeliveryView View(ProofOfDelivery proof, string? capturedByName) =>
        new(
            proof.DeliveryId,
            proof.RecipientName,
            new LocationView(
                new MapLocation(proof.CaptureLocation.Latitude, proof.CaptureLocation.Longitude),
                proof.CaptureLocation.Address),
            proof.CapturedAt,
            capturedByName,

            // Signature first and photographs after, then by id, so the order a screen renders is
            // the order the capture happened in rather than whatever the join returned. An Include
            // promises no ordering at all.
            [.. proof.Assets
                .OrderBy(asset => asset.Kind == ProofAssetKind.Signature ? 0 : 1)
                .ThenBy(asset => asset.Id)
                .Select(asset => new ProofAssetView(asset.Id, asset.Kind, asset.ContentType))]);

    /// <summary>
    /// The one not-found answer for "no such delivery", "not yours" and "no proof yet". One sentence
    /// for three states on purpose: distinguishing them is how a client learns that somebody else's
    /// delivery exists.
    /// </summary>
    private static NotFoundException NotFound(int deliveryId) =>
        new(
            ErrorCode.COMMON_NOT_FOUND,
            "No proof of delivery is readable for delivery "
                + deliveryId.ToString(CultureInfo.InvariantCulture) + ".");

    private static NotFoundException NoSuchAsset(int assetId) =>
        new(
            ErrorCode.COMMON_NOT_FOUND,
            "No proof asset is readable with id "
                + assetId.ToString(CultureInfo.InvariantCulture) + ".");

    private static ConflictException AlreadyCaptured(int deliveryId) =>
        new(
            ErrorCode.DELIVERY_PROOF_ALREADY_CAPTURED,
            "Delivery " + deliveryId.ToString(CultureInfo.InvariantCulture)
                + " already has a proof of delivery, and a proof is never replaced.");
}
