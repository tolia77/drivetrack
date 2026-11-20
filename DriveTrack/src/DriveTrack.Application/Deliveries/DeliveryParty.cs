namespace DriveTrack.Application.Deliveries;

/// <summary>
/// A party to a delivery as a dispatch screen reads one: the row id it is addressed by, and the
/// name a person recognises.
/// <para>
/// Flattened to two scalars rather than nesting a driver or a client DTO, for the reason
/// <c>DriverSummary</c> flattens its vehicle: this is what a table cell shows and what the
/// browser-side filter runs over, so it travels as text.
/// </para>
/// </summary>
/// <param name="Id">The driver row's or client row's own id.</param>
/// <param name="Name">The party's display name, taken from the account behind the row.</param>
public sealed record DeliveryParty(int Id, string Name);
