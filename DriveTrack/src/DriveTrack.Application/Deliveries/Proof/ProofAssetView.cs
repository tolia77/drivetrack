using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// One stored artefact as a caller reads it: an id to fetch it by, what it is, and what it will
/// arrive as.
/// <para>
/// The id, and nothing that resembles a location. The storage key stays inside the system, so
/// this carries no key, no path and no URL — a reader asks the authenticated asset route for the id
/// and the capability decides, every time, whether this caller may have the bytes.
/// </para>
/// </summary>
/// <param name="Id">The asset row's id, which the asset route takes.</param>
/// <param name="Kind">Whether this is the signature or a photograph.</param>
/// <param name="ContentType">What the bytes will arrive as, so a client can choose how to show them.</param>
public sealed record ProofAssetView(int Id, ProofAssetKind Kind, string ContentType);
