using System.Collections.ObjectModel;

namespace DriveTrack.Application.Common.Errors;

/// <summary>
/// One offending field of a rejected request: the CLR property name, and the resource key of the
/// message explaining why. The key, not a message - the adapter resolves it through the same
/// localized catalogue as <c>error.code</c>, so nothing here can put untranslated or caller-derived
/// prose on the wire.
/// <para>
/// <see cref="Field"/> is the name this layer knows (<c>Rating</c>). The adapter converts it to the
/// name the caller sent (<c>rating</c>) with the same naming policy it serializes everything else
/// with; Application does not know how the wire spells its properties and must not guess.
/// </para>
/// </summary>
/// <param name="Field">The offending property name, as the CLR spells it.</param>
/// <param name="MessageKey">Resource key of the explanation.</param>
public sealed record FieldError(string Field, string MessageKey);
