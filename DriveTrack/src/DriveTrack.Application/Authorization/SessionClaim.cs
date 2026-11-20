namespace DriveTrack.Application.Authorization;

/// <summary>One identity claim a scheme issues: a <see cref="DriveTrackClaimTypes"/> name and its value.</summary>
/// <param name="Type">The claim name, one of <see cref="DriveTrackClaimTypes"/>.</param>
/// <param name="Value">The claim value, already in its invariant string form.</param>
public sealed record SessionClaim(string Type, string Value);
