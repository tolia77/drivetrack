using System.Globalization;

namespace DriveTrack.Domain.Identity;

/// <summary>
/// The identity of a client row — deliberately not the identity of the client's user.
/// See <see cref="UserId"/> for why the three identity kinds never convert (AD-22).
/// </summary>
/// <param name="Value">The underlying surrogate key.</param>
public readonly record struct ClientId(int Value)
{
    /// <summary>The bare key, so an id reads as a number wherever it is written into text.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
