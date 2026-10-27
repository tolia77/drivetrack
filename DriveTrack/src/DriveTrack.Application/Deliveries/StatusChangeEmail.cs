using System.Globalization;
using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Resources;
using DriveTrack.Domain.Common;
using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// FR-28's notice, composed here rather than in the transport.
/// <para>
/// <see cref="IEmailSender"/> takes a written message precisely so that this — the only part a
/// client ever reads — stays in the layer that owns the product's vocabulary. An adapter that
/// composed its own subject line would be a second place NFR-14 applied, and the one place nobody
/// would look for it.
/// </para>
/// </summary>
internal static class StatusChangeEmail
{
    /// <summary>The notice that a delivery moved from one state to another.</summary>
    /// <param name="recipient">The client account's address.</param>
    /// <param name="dropoff">
    /// Where the delivery is going. Named by its address because that is what the client sees on
    /// their own list; the row's id never appears on a screen, so a client had nothing to match a
    /// number against.
    /// </param>
    /// <param name="previous">The state it held.</param>
    /// <param name="next">The state it holds now.</param>
    public static EmailMessage For(
        string recipient,
        Location dropoff,
        DeliveryStatus previous,
        DeliveryStatus next) =>
        new(
            recipient,
            string.Format(CultureInfo.CurrentCulture, EmailText.StatusChangeSubject, EmailText.Label(next)),
            string.Format(
                CultureInfo.CurrentCulture,
                EmailText.StatusChangeBody,
                Place(dropoff),
                EmailText.Label(previous),
                EmailText.Label(next)));

    /// <summary>
    /// The address, or the point itself while the geocoder has not named it yet: the notice goes
    /// out on the status change, which does not wait for the lookup.
    /// </summary>
    private static string Place(Location location) =>
        string.IsNullOrWhiteSpace(location.Address)
            ? string.Format(CultureInfo.InvariantCulture, "{0:F5}, {1:F5}", location.Latitude, location.Longitude)
            : location.Address;
}
