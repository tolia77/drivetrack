using System.Linq.Expressions;
using DriveTrack.Infrastructure.Identity;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>
/// The display name as a query spells it: the expression-tree twin of
/// <c>PersonNames.Display</c>, for the projections, filters and sorts that have to name a person
/// inside SQL.
/// <para>
/// A method call cannot be translated, so the rule is restated here once as an expression rather
/// than once per query: every repository that reads a name off an account selects through this,
/// and the trimmed <c>"First Last"</c> the board searches on is the one the services show.
/// </para>
/// </summary>
internal static class AccountNames
{
    /// <summary>The trimmed <c>"First Last"</c> display name of an account.</summary>
    public static readonly Expression<Func<ApplicationUser, string>> Display =
        user => (user.FirstName + " " + user.LastName).Trim();
}
