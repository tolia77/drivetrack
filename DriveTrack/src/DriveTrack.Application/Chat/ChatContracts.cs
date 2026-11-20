using DriveTrack.Domain.Identity;

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

/// <summary>
/// One driver's whole conversation: who it is about, and every line of it oldest first.
/// </summary>
/// <param name="DriverId">The driver row the thread is keyed on.</param>
/// <param name="DriverName">The driver's display name, for the thread header.</param>
/// <param name="Messages">Every message, ordered by <c>SentAt</c> then by id.</param>
public sealed record ChatThread(
    DriverId DriverId,
    string DriverName,
    IReadOnlyList<ChatMessage> Messages);

/// <summary>
/// One row of the dispatch desk's roster of conversations: enough to name a thread, to tell
/// one driver from another at a glance, and to open it. Still deliberately no message count, no
/// preview and no unread count.
/// <para>
/// The three facts beside the name cost nothing. <c>ListThreadsAsync</c> already reads the roster
/// through <c>IDriverService.ListAllAsync</c> and is handed a <c>DriverSummary</c> carrying the
/// vehicle, its plate and whether the driver is on duty; carrying only the name meant discarding
/// them and leaving a dispatcher to tell two people apart by a name they may share. No extra query,
/// no extra round trip — the same rows, read rather than thrown away.
/// </para>
/// </summary>
/// <param name="DriverId">The driver row the thread is keyed on.</param>
/// <param name="DriverName">The driver's display name, which the roster is ordered by.</param>
/// <param name="VehicleModel">
/// The vehicle they are assigned, or null when they have none. Null rather than an empty string:
/// "no vehicle assigned" is a fact about the driver, and a blank line in the roster is not one.
/// </param>
/// <param name="VehicleLicensePlate">That vehicle's plate, or null for the same reason.</param>
/// <param name="OnDuty">Whether a shift of theirs is open right now.</param>
public sealed record ChatThreadSummary(
    DriverId DriverId,
    string DriverName,
    string? VehicleModel,
    string? VehicleLicensePlate,
    bool OnDuty);

/// <summary>
/// A message being sent into a driver's conversation.
/// </summary>
/// <param name="DriverId">The thread it belongs to. Never a user id.</param>
/// <param name="Text">
/// The body. Nullable because a caller can leave a box empty, and an empty box is a refusal to
/// state rather than a value to guess at.
/// </param>
public sealed record SendMessageCommand(DriverId DriverId, string? Text);
