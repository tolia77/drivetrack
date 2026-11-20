using DriveTrack.Application.Abstractions.Persistence;

namespace DriveTrack.Application.Abstractions.Outbound;

/// <summary>
/// Issues the bearer token the REST adapter authenticates with.
/// <para>
/// A port, because the layering rule forbids Application naming a JWT type: the signing algorithm,
/// the key, the claim names and the lifetime are all adapter detail. This layer knows only that an
/// account can be exchanged for an opaque string a client sends back.
/// </para>
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>The signed access token for <paramref name="account"/>.</summary>
    string Issue(UserAccount account);
}
