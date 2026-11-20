using DriveTrack.Application.Common;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// A captured proof as a caller reads it.
/// <para>
/// <see cref="CapturedByName"/> is the disclosure decision, taken in the service's mapping step
/// against the caller and nowhere else: dispatch is told who captured the proof, and a client is
/// told that it was captured. A client learning the assigned driver's name through the evidence
/// would be the same disclosure the client view closes on the delivery itself, arriving through a
/// different door — so the field is null for every role but administrator and dispatcher, and null
/// means withheld rather than missing.
/// </para>
/// </summary>
/// <param name="DeliveryId">The delivery this proves.</param>
/// <param name="RecipientName">Who took the parcel at the door.</param>
/// <param name="CaptureLocation">Where the hand-over happened, as every screen reads a point.</param>
/// <param name="CapturedAt">When it happened, at offset zero.</param>
/// <param name="CapturedByName">The capturer's display name, or null when this caller may not be told.</param>
/// <param name="Assets">The signature and the photographs, signature first.</param>
public sealed record ProofOfDeliveryView(
    int DeliveryId,
    string RecipientName,
    LocationView CaptureLocation,
    DateTimeOffset CapturedAt,
    string? CapturedByName,
    IReadOnlyList<ProofAssetView> Assets);
