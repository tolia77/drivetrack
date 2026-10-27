using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Common;
using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Tests.Deliveries;

/// <summary>
/// What a client reads when their delivery changes state.
/// <para>
/// It names the delivery by where it is going, because that is what the client sees on their own
/// list. It used to quote the row's id, which no screen ever shows - so the one fact meant to tie
/// the mail to a delivery was a number the client had nothing to match against.
/// </para>
/// </summary>
public class StatusChangeEmailTests
{
    [Fact]
    public void The_notice_names_the_delivery_by_its_dropoff_address_and_the_new_state()
    {
        var dropoff = new Location(50.44, 30.52, "вул. Хрещатик, 22, Київ", DateTimeOffset.UnixEpoch);

        var email = StatusChangeEmail.For("client@example.com", dropoff, DeliveryStatus.Pending, DeliveryStatus.InTransit);

        Assert.Equal("client@example.com", email.Recipient);
        Assert.Equal("Ваша доставка: У дорозі", email.Subject);
        Assert.Contains("за адресою вул. Хрещатик, 22, Київ змінено з «Очікує» на «У дорозі»", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("№", email.Subject + email.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dropoff_the_geocoder_has_not_named_yet_is_given_as_its_point()
    {
        var dropoff = new Location(50.4501, 30.5234, null, null);

        var email = StatusChangeEmail.For("client@example.com", dropoff, DeliveryStatus.InTransit, DeliveryStatus.Delivered);

        Assert.Contains("за адресою 50.45010, 30.52340 змінено", email.Body, StringComparison.Ordinal);
    }
}
