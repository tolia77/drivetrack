namespace DriveTrack.Application.Users;

/// <summary>
/// The one spelling of a person's display name: the given name, a space, the family name, trimmed.
/// <para>
/// Every screen, claim and snapshot that names a person by their account goes through here, so two
/// places can never disagree about how the same person reads. The trim is what keeps an account
/// with only one of the two parts from rendering a stray leading or trailing space.
/// </para>
/// <para>
/// The EF projections in Infrastructure spell the same expression inside the query, because a
/// method call cannot be translated to SQL; <c>AccountNames</c> there is their one copy of it.
/// </para>
/// </summary>
public static class PersonNames
{
    /// <summary>The trimmed <c>"First Last"</c> display name.</summary>
    /// <param name="firstName">Given name.</param>
    /// <param name="lastName">Family name.</param>
    public static string Display(string firstName, string lastName) =>
        (firstName + " " + lastName).Trim();
}
