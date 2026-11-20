using DriveTrack.Application.Abstractions.Outbound;
using DriveTrack.Infrastructure;
using DriveTrack.Infrastructure.Email;
using DriveTrack.Integration.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriveTrack.Integration.Tests.Configuration;

/// <summary>
/// The real <see cref="IEmailSender"/>, resolved from the container <c>AddInfrastructure</c> builds
/// and pointed at a loopback relay.
/// <para>
/// The counterpart of <c>GeocoderAdapterTests</c>, and it exists for the same reason. Every suite
/// about status-change email replaces this port with a fake and asserts what the system recorded,
/// which is right — but it leaves the transport itself unexecuted, and every way it can be wrong is
/// silent from there. Address the message to the from-address, swap the subject for the body, drop
/// the recipient: the whole solution stays green, and the notification log records a confident
/// <c>Sent</c> for mail that went somewhere else. So the envelope this adapter actually puts on the
/// wire is read back here.
/// </para>
/// <para>
/// No database and no container: <c>AddInfrastructure</c> needs a connection string to be present,
/// not to be reachable, and nothing here opens one.
/// </para>
/// </summary>
public class EmailAdapterTests
{
    private const string ValidConnectionString =
        "Host=localhost;Port=5432;Database=drivetrack;Username=drivetrack;Password=irrelevant";

    private const string FromAddress = "no-reply@drivetrack.test";

    [Fact]
    public async Task A_configured_relay_receives_the_message_addressed_to_its_recipient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var relay = LoopbackSmtpServer.Start();

        using var provider = Resolve(relay);

        var sender = provider.GetRequiredService<IEmailSender>();

        await sender.SendAsync(
            new EmailMessage("olena@drivetrack.test", "Статус доставки змінено", "Ваша доставка у дорозі."),
            cancellationToken);

        var message = Assert.Single(relay.Messages);

        // The envelope. The recipient is the port's, not the configured from-address - which is the
        // one-character mistake that would send every client's notice to the operator's own mailbox
        // while the attempt row still named the client.
        Assert.Equal(FromAddress, message.From);
        Assert.Equal("olena@drivetrack.test", Assert.Single(message.Recipients));

        // The body is the body, decoded back through the transfer encoding a Ukrainian message needs.
        Assert.Equal("Ваша доставка у дорозі.", message.Text.Trim());

        // And the subject is a header rather than the first line of the text. Both cross the wire
        // encoded, so what is asserted is that a Subject header exists and that the body did not end
        // up in it - the swap that would otherwise ship with every test still green.
        Assert.Contains("Subject:", message.Headers, StringComparison.Ordinal);
        Assert.DoesNotContain("Ваша доставка", message.Headers, StringComparison.Ordinal);

        // Plain text, declared as such: the body is composed from a resx template with no markup in
        // it, and declaring HTML would make a stray angle bracket in an address a rendering bug.
        Assert.Contains("text/plain", message.Headers, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_credentials_reach_a_relay_that_asks_for_them()
    {
        // SmtpClient presents credentials only to a relay that advertises AUTH, so against the
        // default loopback relay Smtp:User and Smtp:Password would never leave the process.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var relay = LoopbackSmtpServer.Start(advertiseAuth: true);

        using var provider = Resolve(relay, user: "relay-user", password: "relay-пароль-1");

        await provider.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage("olena@drivetrack.test", "Тема", "Текст"),
            cancellationToken);

        var credential = Assert.Single(relay.Credentials);

        Assert.Equal("relay-user", credential.User);
        Assert.Equal("relay-пароль-1", credential.Password);

        // Authenticated, and then still delivered.
        Assert.Single(relay.Messages);
    }

    [Fact]
    public async Task A_blank_user_attempts_no_authentication_even_when_the_relay_offers_it()
    {
        // A development relay accepts anonymous submission; handing it empty credentials would be a
        // failed AUTH rather than no AUTH.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var relay = LoopbackSmtpServer.Start(advertiseAuth: true);

        using var provider = Resolve(relay, user: string.Empty, password: "ignored-without-a-user");

        await provider.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage("olena@drivetrack.test", "Тема", "Текст"),
            cancellationToken);

        Assert.Empty(relay.Credentials);
        Assert.Single(relay.Messages);
    }

    /// <summary>
    /// The registered sender, pointed at the relay, built through <c>AddInfrastructure</c> so the
    /// options binding is the one the application runs rather than a hand-assembled approximation.
    /// </summary>
    /// <param name="relay">The loopback relay to send to.</param>
    /// <param name="user">Smtp:User, or null to leave it unset.</param>
    /// <param name="password">Smtp:Password, or null to leave it unset.</param>
    private static ServiceProvider Resolve(
        LoopbackSmtpServer relay,
        string? user = null,
        string? password = null)
    {
        var values = TestConfiguration.Defaults();
        values["ConnectionStrings:Default"] = ValidConnectionString;
        values["Smtp:Host"] = "127.0.0.1";
        values[SmtpOptions.PortConfigurationKey] = relay.PortSetting;
        values[SmtpOptions.UseStartTlsConfigurationKey] = "false";
        values["Smtp:FromAddress"] = FromAddress;
        values["Smtp:FromName"] = "DriveTrack";

        if (user is not null)
        {
            values["Smtp:User"] = user;
        }

        if (password is not null)
        {
            values["Smtp:Password"] = password;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment());

        return services.BuildServiceProvider();
    }
}
