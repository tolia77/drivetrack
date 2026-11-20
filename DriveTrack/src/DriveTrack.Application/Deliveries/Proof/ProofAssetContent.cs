namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// One asset's bytes, on their way out to whoever asked for them and was allowed to.
/// <para>
/// A stream rather than an array, so an image is written to the response as it is read from the
/// store instead of being held whole in memory first. The caller disposes it, which for the asset
/// route means the framework does once the response has been written.
/// </para>
/// </summary>
/// <param name="ContentType">The stored MIME type, so the response needs no sniffing.</param>
/// <param name="Content">The bytes. The caller disposes the stream.</param>
public sealed record ProofAssetContent(string ContentType, Stream Content);
