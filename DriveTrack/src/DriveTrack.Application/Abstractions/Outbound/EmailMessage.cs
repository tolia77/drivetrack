namespace DriveTrack.Application.Abstractions.Outbound;

/// <summary>
/// One outbound message, already worded.
/// </summary>
/// <param name="Recipient">The address to send to.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
public sealed record EmailMessage(string Recipient, string Subject, string Body);
