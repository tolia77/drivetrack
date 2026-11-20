using DriveTrack.Application.Common;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// A request to record that a parcel changed hands.
/// <para>
/// There is no delivery id here: the id is the route, as it is for every other single-row operation
/// in this capability. There is no captured-at either — the injected clock supplies it, and a
/// caller who could name the instant could name any instant, which is the one thing evidence must
/// not permit.
/// </para>
/// <para>
/// The coordinates arrive as a <see cref="LocationInput"/> and have no fallback: a proof records
/// where the hand-over happened, read from the device rather than typed, and inventing a substitute —
/// the dropoff point, say — would record a place the capture did not happen.
/// </para>
/// </summary>
/// <param name="RecipientName">Who took the parcel at the door.</param>
/// <param name="CaptureLocation">Where the hand-over happened.</param>
/// <param name="Assets">The signature and the photographs, in any order.</param>
public sealed record CaptureProofCommand(
    string? RecipientName,
    LocationInput? CaptureLocation,
    IReadOnlyList<ProofAssetUpload>? Assets);
