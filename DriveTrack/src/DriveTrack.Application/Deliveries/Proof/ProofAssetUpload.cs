using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// One artefact on its way into the store: what it is, what it claims to be, how big it claims to
/// be, and the bytes themselves.
/// <para>
/// <see cref="Content"/> is a <see cref="Stream"/> and not a byte array, so a five-megabyte
/// photograph is not copied onto the large-object heap on its way through three layers. It is an
/// <em>ASP.NET-free</em> stream: <c>IFormFile</c> and <c>IBrowserFile</c> stop at the adapter, which
/// is what keeps this command — and everything below it — free of a web framework.
/// </para>
/// <para>
/// <see cref="Length"/> is declared rather than measured, because the streams this carries are
/// forward-only and a validator that measured one would consume the bytes the store is about to
/// write. Both adapters know the length before they open the stream, and the upload check has to
/// run before the first byte reaches the store rather than after.
/// </para>
/// <para>
/// Because a stream is consumed by being read, a command is single-use: it is a message, not a value
/// to keep. Nothing retries one.
/// </para>
/// </summary>
/// <param name="Kind">Whether this is the signature or a photograph.</param>
/// <param name="ContentType">The MIME type the caller declared, or null when it declared none.</param>
/// <param name="Length">The length in bytes the caller declared.</param>
/// <param name="Content">The bytes. The caller owns the stream and disposes it.</param>
public sealed record ProofAssetUpload(
    ProofAssetKind Kind,
    string? ContentType,
    long Length,
    Stream Content);
