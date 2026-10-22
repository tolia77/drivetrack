namespace DriveTrack.Application.Dashboard;

/// <summary>
/// The dispatcher's and the administrator's first screen: the day at a glance. Read-only.
/// </summary>
public interface IDashboardService
{
    /// <summary>Reads the dashboard as the current caller may see it.</summary>
    /// <exception cref="Common.ForbiddenException">
    /// The caller is neither a dispatcher nor an administrator (<c>AUTH_FORBIDDEN</c>), or has no
    /// session left (<c>AUTH_UNAUTHENTICATED</c>).
    /// </exception>
    Task<DashboardView> GetAsync(CancellationToken cancellationToken);
}
