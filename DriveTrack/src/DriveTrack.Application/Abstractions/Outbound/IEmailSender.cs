namespace DriveTrack.Application.Abstractions.Outbound;

/// <summary>
/// The outbound-mail port. A dumb transport and nothing more.
/// <para>
/// The wording is the caller's: this takes a composed <see cref="EmailMessage"/> rather than a
/// delivery and a status, so no user-facing sentence lives in an adapter and the text catalogue
/// stays in the layer that owns it.
/// </para>
/// <para>
/// A failure is thrown, not swallowed. Recording an attempt is the caller's job — the row an
/// administrator reads is a <c>NotificationAttempt</c>, and a transport that quietly returned
/// false would make "sent" and "not sent" indistinguishable at the only place that can write one.
/// </para>
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends the message, throwing when it could not be handed over.</summary>
    /// <param name="message">The composed message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
