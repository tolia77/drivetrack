namespace DriveTrack.Application.Abstractions;

/// <summary>
/// A place a forward lookup found: what it is called, and where it is.
/// </summary>
/// <param name="Address">The human-readable address the provider returned.</param>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
public sealed record GeocodedPlace(string Address, double Latitude, double Longitude);

/// <summary>
/// The geocoding port, in both directions: coordinates to an address and a typed
/// query to a list of places.
/// <para>
/// The original called out to a geocoding service from inside <c>Location.get_address()</c> — a
/// model class issuing a network request, which an entity must never do. The capability lives here
/// instead, behind an interface Application owns and Infrastructure implements, and the reverse
/// direction is called after the transaction rather than during a render.
/// </para>
/// <para>
/// Neither method is allowed to be the reason an operation fails. A provider that is down, slow or
/// unhelpful answers <c>null</c> and an empty list respectively (the coordinates are
/// authoritative and the address is a cache), so the only failure an adapter may raise is one the
/// caller could do something about — and there is none.
/// </para>
/// </summary>
public interface IGeocoder
{
    /// <summary>
    /// The address at those coordinates, or null when the provider has none or cannot be reached.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> DescribeAsync(double latitude, double longitude, CancellationToken cancellationToken);

    /// <summary>
    /// The places matching a typed query, newest provider ranking first, or an empty list when
    /// there are none.
    /// </summary>
    /// <param name="query">What the dispatcher typed. Already validated by the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<GeocodedPlace>> SearchAsync(string query, CancellationToken cancellationToken);
}
