namespace DriveTrack.Application.Dashboard;

/// <summary>One of the newest reviews.</summary>
/// <param name="Id">The review's id.</param>
/// <param name="Rating">The score, 1 to 5.</param>
/// <param name="ClientName">Who wrote it.</param>
/// <param name="DriverName">The driver it is about, or null when the delivery had none.</param>
/// <param name="Text">What it says.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record DashboardReview(
    int Id,
    int Rating,
    string ClientName,
    string? DriverName,
    string Text,
    DateTimeOffset CreatedAt);
