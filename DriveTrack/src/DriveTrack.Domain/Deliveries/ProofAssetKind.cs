namespace DriveTrack.Domain.Deliveries;

/// <summary>
/// What a stored proof asset is. Persisted as the member name.
/// </summary>
public enum ProofAssetKind
{
    /// <summary>The recipient's signature.</summary>
    Signature,

    /// <summary>A photograph taken at the door.</summary>
    Photo,
}
