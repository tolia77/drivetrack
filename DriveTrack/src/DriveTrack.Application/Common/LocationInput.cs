using FluentValidation;

namespace DriveTrack.Application.Common;

/// <summary>
/// A point as a caller sends one: two nullable numbers and nothing else.
/// <para>
/// The obvious shape would have been <see cref="MapLocation"/> itself, and it is exactly the wrong
/// one on the way in. <see cref="MapLocation"/>'s constructor throws
/// <see cref="ArgumentOutOfRangeException"/> for a coordinate no map could show, and a constructor
/// that throws during model binding lands in the suppressed model state and leaves as a 500 — so a
/// latitude of 91 would answer "unexpected error" where the contract promises a 422 naming the
/// field. Binding to this pair instead lets <see cref="LocationInputValidator"/> refuse the same
/// values with a field name attached, and <see cref="MapLocation"/> stays the shape that leaves.
/// </para>
/// <para>
/// The two are nullable so an omitted coordinate is absent rather than zero: <c>0, 0</c> is a real
/// point in the Atlantic, and a payload that forgot its longitude must not quietly become one.
/// </para>
/// </summary>
/// <param name="Latitude">Latitude in decimal degrees, or null when the caller sent none.</param>
/// <param name="Longitude">Longitude in decimal degrees, or null when the caller sent none.</param>
public sealed record LocationInput(double? Latitude, double? Longitude);
