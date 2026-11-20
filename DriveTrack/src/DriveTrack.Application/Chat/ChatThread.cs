using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Chat;

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
