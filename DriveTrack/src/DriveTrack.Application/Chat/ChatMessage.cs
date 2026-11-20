namespace DriveTrack.Application.Chat;

/// <summary>
/// One line of a conversation, as a caller sees it (the entity never crosses the boundary).
/// <para>
/// <see cref="SenderUserId"/> is a plain <see cref="int"/> rather than a <c>UserId</c> on purpose.
/// It exists so a viewer can recognise their own lines; nothing authorizes on it, and a typed
/// identity here would only be a second wire shape for a number the screen compares against the one
/// it already holds.
/// </para>
/// </summary>
/// <param name="Id">The message row's id, which is also its tie-break in the thread order.</param>
/// <param name="SenderUserId">Who wrote it, or null once that account has been deleted.</param>
/// <param name="SenderName">
/// The sender's display name, or the empty string when the account is gone. Empty rather than a
/// sentence: the unattributed label is a rendered string and belongs in the localized catalogue,
/// not in a layer that has no culture.
/// </param>
/// <param name="SentAt">When it was sent, at offset zero.</param>
/// <param name="Text">The message body, exactly as it was stored.</param>
public sealed record ChatMessage(
    int Id,
    int? SenderUserId,
    string SenderName,
    DateTimeOffset SentAt,
    string Text);
