namespace DriveTrack.Application.Common;

/// <summary>
/// One page of a list, and how many rows the whole filtered list holds (NFR-27).
/// <para>
/// Every list in the product answers this shape, so a screen's pager and a REST caller read the same
/// four facts the same way. <see cref="Total"/> counts the set the page was cut from - after the
/// caller's access scope and every filter, before OFFSET and LIMIT - so "41–50 of 86" is a claim
/// about rows the caller could actually page to, never about rows they may not see (AD-3).
/// </para>
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Items">The rows on this page, in the order asked for.</param>
/// <param name="Total">How many rows match across every page.</param>
/// <param name="Offset">The offset this page was read at.</param>
/// <param name="Limit">The page size this page was read with.</param>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Total, int Offset, int Limit)
{
    /// <summary>The same page with every row mapped, and the counts carried across unchanged.</summary>
    /// <typeparam name="TResult">The mapped row type.</typeparam>
    /// <param name="map">How one row becomes the other.</param>
    public Paged<TResult> Map<TResult>(Func<T, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return new Paged<TResult>([.. Items.Select(map)], Total, Offset, Limit);
    }
}
