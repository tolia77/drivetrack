using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Dashboard;

/// <summary>The newest message of one driver's conversation.</summary>
/// <param name="DriverId">The driver the conversation is keyed on.</param>
/// <param name="DriverName">The driver's display name.</param>
/// <param name="Text">What the newest message says.</param>
/// <param name="SentAt">When it was sent.</param>
public sealed record DashboardConversation(
    DriverId DriverId,
    string DriverName,
    string Text,
    DateTimeOffset SentAt);
