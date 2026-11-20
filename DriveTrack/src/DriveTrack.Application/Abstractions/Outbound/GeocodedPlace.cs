namespace DriveTrack.Application.Abstractions.Outbound;

/// <summary>
/// A place a forward lookup found: what it is called, and where it is.
/// </summary>
/// <param name="Address">The human-readable address the provider returned.</param>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
public sealed record GeocodedPlace(string Address, double Latitude, double Longitude);
