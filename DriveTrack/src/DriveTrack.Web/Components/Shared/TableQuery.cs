using System.Globalization;
using DriveTrack.Application.Common;
using Microsoft.AspNetCore.Components;

namespace DriveTrack.Web.Components.Shared;

/// <summary>
/// How a paged table's view is written into, and read back out of, the address bar.
/// <para>
/// Page, page size, sort and filters live in the query string, so a reload, Back and a shared link
/// all reproduce the same rows. Every value is read as text and parsed here rather than bound as a
/// typed <c>[SupplyParameterFromQuery]</c>: the framework throws on a value it cannot convert, and a
/// hand-edited <c>?page=abc</c> must fall back to the default rather than take the screen down.
/// </para>
/// <para>
/// A value equal to its default is left out of the address entirely, so the plain route is the
/// default view and a link only carries what somebody actually chose.
/// </para>
/// </summary>
internal static class TableQuery
{
    /// <summary>The page size a table opens at (design system, Pagination).</summary>
    public const int DefaultPageSize = 10;

    /// <summary>The page sizes the rows-per-page select offers.</summary>
    public static readonly IReadOnlyList<int> PageSizes = [10, 25, 50, 100];

    /// <summary>The query-string names every paged table shares.</summary>
    public const string PageName = "page";

    /// <inheritdoc cref="PageName" />
    public const string SizeName = "size";

    /// <inheritdoc cref="PageName" />
    public const string SortName = "sort";

    /// <inheritdoc cref="PageName" />
    public const string DescendingName = "desc";

    /// <inheritdoc cref="PageName" />
    public const string SearchName = "q";

    /// <summary>
    /// The highest page an address may name. Far beyond any list this product holds, and low enough
    /// that the offset it implies cannot overflow at the largest page size.
    /// </summary>
    private const int MaximumPage = 1_000_000;

    /// <summary>The one-based page an address names, or the first page.</summary>
    public static int Page(string? raw) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var page)
            && page is >= 1 and <= MaximumPage
            ? page
            : 1;

    /// <summary>The page size an address names, or the default when it names one the select does not offer.</summary>
    public static int Size(string? raw) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            && PageSizes.Contains(size)
            ? size
            : DefaultPageSize;

    /// <summary>The offset of a one-based page.</summary>
    public static int Offset(int page, int size) => (Math.Max(page, 1) - 1) * size;

    /// <summary>The one-based page an offset falls on.</summary>
    public static int PageOf(int offset, int size) => size <= 0 ? 1 : (offset / size) + 1;

    /// <summary>
    /// The column an address sorts by, or <paramref name="fallback"/> when it names none this screen
    /// offers. A column the list has but this screen does not draw a heading for is refused the
    /// same way, so the address cannot sort a table by something it does not show.
    /// </summary>
    public static TColumn Sort<TColumn>(string? raw, TColumn fallback, IReadOnlyCollection<TColumn> offered)
        where TColumn : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(offered);

        return ListSort.Parse<TColumn>(raw) is { } column && offered.Contains(column) ? column : fallback;
    }

    /// <summary>
    /// The direction an address sorts in: <c>desc=true</c> or <c>desc=false</c>, and
    /// <paramref name="fallback"/> - the screen's own direction for its default column - otherwise.
    /// </summary>
    public static bool Direction(string? raw, bool fallback)
    {
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fallback;
    }

    /// <summary>The address form of a direction, or null for the screen's default one.</summary>
    public static string? DirectionValue(bool descending, bool fallback) =>
        descending == fallback ? null : descending ? "true" : "false";

    /// <summary>A true/false switch: on only for <c>true</c> or <c>1</c>.</summary>
    public static bool Flag(string? raw) =>
        string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "1", StringComparison.Ordinal);

    /// <summary>
    /// A text filter as typed, or null when it is blank. Not trimmed: the box is filled back from
    /// this value, and trimming it would move the caret while somebody is still typing. The service
    /// trims before it searches.
    /// </summary>
    public static string? Text(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw;

    /// <summary>A date filter in ISO form, or null.</summary>
    public static DateOnly? Date(string? raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    /// <summary>An enum filter by member name, or null.</summary>
    public static TEnum? Member<TEnum>(string? raw)
        where TEnum : struct, Enum =>
        ListSort.Parse<TEnum>(raw);

    /// <summary>The address form of a page, or null for the first.</summary>
    public static string? PageValue(int page) =>
        page <= 1 ? null : page.ToString(CultureInfo.InvariantCulture);

    /// <summary>The address form of a page size, or null for the default.</summary>
    public static string? SizeValue(int size) =>
        size == DefaultPageSize ? null : size.ToString(CultureInfo.InvariantCulture);

    /// <summary>The address form of a sort column, or null for the screen's default.</summary>
    public static string? SortValue<TColumn>(TColumn column, TColumn fallback)
        where TColumn : struct, Enum =>
        EqualityComparer<TColumn>.Default.Equals(column, fallback) ? null : column.ToString();

    /// <summary>The address form of a switch, or null when it is off.</summary>
    public static string? FlagValue(bool on) => on ? "true" : null;

    /// <summary>The address form of a date, or null.</summary>
    public static string? DateValue(DateOnly? day) =>
        day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// The page to fall back to when a page came back empty but the list is not: the last page that
    /// exists, or null when nothing needs to move. This is what a delete of the only row on the last
    /// page lands on.
    /// </summary>
    public static int? FallbackPage<T>(Paged<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Items.Count > 0 || page.Total == 0 || page.Offset == 0)
        {
            return null;
        }

        return Math.Max(PageWindow.Count(page.Total, page.Limit), 1);
    }

    /// <summary>
    /// Writes a view into the address bar.
    /// <para>
    /// <paramref name="replace"/> is for typing in a search box, where every keystroke is a change
    /// and none of them is a place anybody wants Back to stop at; a sort, a page or a page size is a
    /// history entry of its own.
    /// </para>
    /// </summary>
    public static void Navigate(
        NavigationManager navigation,
        IReadOnlyDictionary<string, object?> parameters,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        navigation.NavigateTo(navigation.GetUriWithQueryParameters(parameters), replace: replace);
    }
}

/// <summary>
/// Which view a table is showing, and which of the address changes arriving were made by the table
/// itself.
/// <para>
/// The screen is the source of truth while somebody is using it: a sort, a page or a keystroke in a
/// search box changes the view here, loads it, and writes it to the address bar. The address change
/// then comes back as a parameter set, possibly after the next keystroke has already moved the view
/// on - and adopting it then would put the older text back into the box under the reader's fingers.
/// So a view this table wrote itself is recognised and ignored when it arrives, and only a view it
/// did not write - Back, Forward, a link, a reload - is adopted and loaded.
/// </para>
/// </summary>
/// <typeparam name="TView">The screen's request: a record, compared by value.</typeparam>
internal sealed class TableView<TView>
    where TView : class
{
    private readonly List<TView> _written = [];

    /// <summary>The view on screen, or being loaded onto it.</summary>
    public TView? Current { get; private set; }

    /// <summary>
    /// The address said <paramref name="fromAddress"/>. True when the screen has to adopt it and
    /// load it; false when it is this table's own write coming back, or the view already on screen.
    /// </summary>
    public bool Arrived(TView fromAddress)
    {
        ArgumentNullException.ThrowIfNull(fromAddress);

        var index = _written.IndexOf(fromAddress);

        if (index >= 0)
        {
            _written.RemoveRange(0, index + 1);

            return false;
        }

        if (Equals(fromAddress, Current))
        {
            return false;
        }

        Current = fromAddress;

        return true;
    }

    /// <summary>The screen moved the view itself and is about to write it to the address bar.</summary>
    public void Moved(TView next)
    {
        ArgumentNullException.ThrowIfNull(next);

        Current = next;
        _written.Add(next);
    }
}

/// <summary>
/// What a sortable column heading says about itself: its ARIA state and its glyph.
/// <para>
/// Every heading that sorts carries <c>aria-sort</c> - "none" on the columns the rows are not in
/// order by, rather than no attribute - and the stacked pair that means "this sorts"; the one the
/// rows are actually ordered by swaps the pair for a single chevron pointing the way it went, so the
/// two directions differ by shape and not by the brand colour alone. The three ARIA values are
/// tokens a browser reads out in the user's own language, so they take no resource key (NFR-14).
/// </para>
/// </summary>
internal static class SortHeading
{
    /// <summary>The heading's <c>aria-sort</c> value.</summary>
    /// <param name="sorted">True when the rows are ordered by this column.</param>
    /// <param name="descending">True when that order is descending.</param>
    public static string State(bool sorted, bool descending)
    {
        if (!sorted)
        {
            return "none";
        }

        return descending ? "descending" : "ascending";
    }

    /// <summary>The heading's glyph.</summary>
    /// <param name="sorted">True when the rows are ordered by this column.</param>
    /// <param name="descending">True when that order is descending.</param>
    public static IconName Glyph(bool sorted, bool descending)
    {
        if (!sorted)
        {
            return IconName.Sort;
        }

        return descending ? IconName.SortDescending : IconName.SortAscending;
    }
}
