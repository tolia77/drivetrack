namespace DriveTrack.Application.Common;

/// <summary>
/// The sort column a list query names, read from the string a caller sent.
/// <para>
/// A query carries its sort as text because that is what a URL carries: a REST caller writes
/// <c>?sort=status</c>, and the validator has to be the one that refuses <c>?sort=bogus</c> with
/// <see cref="ErrorCode.COMMON_SORT_INVALID"/> rather than a model binder dropping it silently. The
/// accepted spellings are the column enum's member names, compared without regard to case.
/// </para>
/// <para>
/// Names only. <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> also accepts a number -
/// <c>"3"</c>, or <c>"42"</c> for a member that does not exist - so it is not used: a sort a caller
/// can only express by knowing the enum's declaration order is not part of any contract.
/// </para>
/// </summary>
public static class ListSort
{
    /// <summary>
    /// True when <paramref name="value"/> is absent or names a column of <typeparamref name="TColumn"/>.
    /// Blank counts as absent: <c>?sort=</c> is a caller who did not choose, not one who chose wrong.
    /// </summary>
    /// <typeparam name="TColumn">The list's column enum.</typeparam>
    /// <param name="value">What the caller sent.</param>
    public static bool IsKnown<TColumn>(string? value)
        where TColumn : struct, Enum =>
        string.IsNullOrWhiteSpace(value) || TryParse<TColumn>(value, out _);

    /// <summary>
    /// The column <paramref name="value"/> names, or null when it names none - which after
    /// validation means the caller sent no sort and the list's own default order applies.
    /// </summary>
    /// <typeparam name="TColumn">The list's column enum.</typeparam>
    /// <param name="value">What the caller sent.</param>
    public static TColumn? Parse<TColumn>(string? value)
        where TColumn : struct, Enum =>
        TryParse<TColumn>(value, out var column) ? column : null;

    private static bool TryParse<TColumn>(string? value, out TColumn column)
        where TColumn : struct, Enum
    {
        column = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var name = Array.Find(
            Enum.GetNames<TColumn>(),
            candidate => string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            return false;
        }

        column = Enum.Parse<TColumn>(name);

        return true;
    }
}
