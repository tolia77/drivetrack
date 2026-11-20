using Microsoft.AspNetCore.Identity;

namespace DriveTrack.Infrastructure.Identity;

/// <summary>
/// The account behind every actor in the system.
/// <para>
/// It lives in Infrastructure, not Domain, because ASP.NET Core Identity is an infrastructure
/// concern and the layering keeps Domain free of every package reference. Domain entities reach a user
/// only through a <c>UserId</c>, never through this type.
/// </para>
/// <para>
/// It carries a name and nothing else; authentication, roles and seeding live elsewhere.
/// </para>
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>Given name, shown wherever a person is named.</summary>
    public required string FirstName { get; set; }

    /// <summary>Family name, shown wherever a person is named.</summary>
    public required string LastName { get; set; }
}
