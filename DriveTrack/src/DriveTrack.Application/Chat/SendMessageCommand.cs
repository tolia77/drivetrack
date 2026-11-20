
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Application.Chat;

/// <summary>
/// A message being sent into a driver's conversation.
/// </summary>
/// <param name="DriverId">The thread it belongs to. Never a user id.</param>
/// <param name="Text">
/// The body. Nullable because a caller can leave a box empty, and an empty box is a refusal to
/// state rather than a value to guess at.
/// </param>
public sealed record SendMessageCommand(DriverId DriverId, string? Text);
