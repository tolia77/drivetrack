namespace DriveTrack.Domain.Notifications;

/// <summary>
/// What a notification attempt was about. Only one kind exists because the system sends only one
/// notification — a status change, by email. A speculative second member would be dead weight.
/// </summary>
public enum NotificationKind
{
    /// <summary>The delivery's status changed.</summary>
    StatusChange,
}
