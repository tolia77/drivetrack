using DriveTrack.Application.Common;
using FluentValidation;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// The address search, as a request rather than a loose string.
/// <para>
/// Nullable, because the wire can omit it and the query has to carry that absence as far as the
/// refusal — the same shape every other command here takes for the same reason (the
/// absent-versus-null argument, on a required field).
/// </para>
/// </summary>
/// <param name="Query">What the dispatcher typed.</param>
public sealed record SearchPlacesQuery(string? Query);
