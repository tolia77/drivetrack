using System.Linq.Expressions;
using DriveTrack.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence;

/// <summary>
/// The pieces every paged list query in this folder is built from: a sort in either direction, a
/// case-insensitive substring match, an enum ordered by its declaration rather than by its stored
/// text, and the page itself with its total.
/// <para>
/// One place, because the rules are the same rules on ten lists. Filter, sort and page are all SQL:
/// the filters are a WHERE applied after the caller's scope, the total counts that same filtered set,
/// and the ORDER BY a repository builds always ends with the row id so OFFSET can never repeat or
/// skip a row between two requests.
/// </para>
/// </summary>
internal static class ListQueries
{
    /// <summary>The escape character every <see cref="Contains"/> pattern is written with.</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Orders by <paramref name="key"/>, ascending or descending.</summary>
    public static IOrderedQueryable<T> SortBy<T, TKey>(
        this IQueryable<T> rows,
        Expression<Func<T, TKey>> key,
        bool descending) =>
        descending ? rows.OrderByDescending(key) : rows.OrderBy(key);

    /// <summary>Adds <paramref name="key"/> to an ordering, ascending or descending.</summary>
    public static IOrderedQueryable<T> ThenSortBy<T, TKey>(
        this IOrderedQueryable<T> rows,
        Expression<Func<T, TKey>> key,
        bool descending) =>
        descending ? rows.ThenByDescending(key) : rows.ThenBy(key);

    /// <summary>
    /// Orders by a nullable key with the rows that have none at the far end in both directions.
    /// <para>
    /// "No value" is neither the smallest nor the largest thing in a column - an undated window, an
    /// unrated driver - so it must not jump to the top when the direction flips, which is what
    /// PostgreSQL's own NULLS FIRST on a descending sort would do.
    /// </para>
    /// </summary>
    public static IOrderedQueryable<T> SortNullsLastBy<T, TKey>(
        this IQueryable<T> rows,
        Expression<Func<T, TKey>> key,
        bool descending)
    {
        var isNull = Expression.Lambda<Func<T, bool>>(
            Expression.Equal(key.Body, Expression.Constant(null, key.Body.Type)),
            key.Parameters);

        return rows.OrderBy(isNull).ThenSortBy(key, descending);
    }

    /// <summary>
    /// The ILIKE pattern for "contains <paramref name="term"/>", with the term's own <c>%</c>,
    /// <c>_</c> and escape character escaped so a user typing them searches for them literally.
    /// </summary>
    /// <param name="term">What the user typed. The caller has already dropped a blank one.</param>
    public static string Contains(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var escaped = term.Trim()
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);

        return "%" + escaped + "%";
    }

    /// <summary>The term a text filter narrows by, or null when the box was left blank.</summary>
    public static string? Term(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// An enum column as its position in the enum's declaration, for ordering.
    /// <para>
    /// The enums are stored as their names (AD-21), so an ORDER BY on the column itself sorts them
    /// alphabetically - "Delivered" before "Pending" - which is an order nobody reading a lifecycle
    /// would recognise. This writes the declaration order out as a CASE expression instead.
    /// </para>
    /// </summary>
    public static Expression<Func<T, int>> Rank<T, TEnum>(Expression<Func<T, TEnum>> column)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(column);

        var values = Enum.GetValues<TEnum>();
        Expression ranked = Expression.Constant(values.Length);

        for (var index = values.Length - 1; index >= 0; index--)
        {
            ranked = Expression.Condition(
                Expression.Equal(column.Body, Expression.Constant(values[index])),
                Expression.Constant(index),
                ranked);
        }

        return Expression.Lambda<Func<T, int>>(ranked, column.Parameters);
    }

    /// <summary>
    /// One page of an already filtered query, with the size of the whole filtered set.
    /// <para>
    /// Two statements - a COUNT and the page - in the unit of work's one transaction. The count is
    /// asked of the filtered query before it is ordered, so it is the same WHERE the page is cut
    /// from; an offset past the end answers an empty page with the true total, which is what lets a
    /// screen whose last row was just deleted step back to the last page that exists.
    /// </para>
    /// </summary>
    public static async Task<Paged<T>> ToPageAsync<T>(
        this IQueryable<T> filtered,
        Func<IQueryable<T>, IOrderedQueryable<T>> order,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var total = await filtered.CountAsync(cancellationToken);

        if (total == 0 || offset >= total)
        {
            return new Paged<T>([], total, offset, limit);
        }

        var rows = await order(filtered)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new Paged<T>(rows, total, offset, limit);
    }
}
