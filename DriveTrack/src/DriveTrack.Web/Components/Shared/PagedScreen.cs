using DriveTrack.Application.Common;
using Microsoft.AspNetCore.Components;

namespace DriveTrack.Web.Components.Shared;

/// <summary>
/// A screen built around one paged table: the view it shows lives in the address bar, the server
/// answers it a page at a time, and the pager and the sortable headings move it.
/// <para>
/// Owned once so ten screens cannot get it ten ways. What every one of them needs is the same:
/// read page, size, sort and direction out of the query string without failing on a value nobody
/// can parse; load that view; when a heading, the pager or a search box moves the view, write it
/// back to the address - replacing the entry for a keystroke, pushing one for anything else - and
/// load it; return to page one on any change but the page itself; ignore a load that a newer one
/// has overtaken; and when a page comes back empty but the list is not, step back to the last page
/// that exists.
/// </para>
/// <para>
/// What a screen still says for itself is its own view type, which columns it offers to sort by,
/// how to read its filters out of the address and write them back, and which service to ask.
/// </para>
/// </summary>
/// <typeparam name="TView">The screen's request - usually the service's own list query. A record.</typeparam>
/// <typeparam name="TRow">The row type the service answers.</typeparam>
/// <typeparam name="TColumn">The screen's sort columns.</typeparam>
public abstract class PagedScreen<TView, TRow, TColumn> : CircuitScopedComponent
    where TView : class
    where TColumn : struct, Enum
{
    private readonly TableView<TView> _view = new();

    // Whether the last move replaced the history entry, which is what a run of keystrokes does.
    private bool _typing;

    // Which load is the latest. A load overtaken by a newer one - a fast typist, a double click on
    // the pager - finishes into nothing rather than overwriting the newer page.
    private int _version;

    /// <summary>Writes the view into the address bar.</summary>
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>The one-based page, as the address carries it.</summary>
    [SupplyParameterFromQuery(Name = TableQuery.PageName)]
    public string? PageParameter { get; set; }

    /// <summary>The page size, as the address carries it.</summary>
    [SupplyParameterFromQuery(Name = TableQuery.SizeName)]
    public string? SizeParameter { get; set; }

    /// <summary>The sort column, as the address carries it.</summary>
    [SupplyParameterFromQuery(Name = TableQuery.SortName)]
    public string? SortParameter { get; set; }

    /// <summary>The sort direction, as the address carries it.</summary>
    [SupplyParameterFromQuery(Name = TableQuery.DescendingName)]
    public string? DescendingParameter { get; set; }

    /// <summary>The search box, as the address carries it. Unused by a screen with no search box.</summary>
    [SupplyParameterFromQuery(Name = TableQuery.SearchName)]
    public string? SearchParameter { get; set; }

    /// <summary>
    /// The page last received - which is what the table and the pager draw, including while the
    /// next one loads. Null until the first page has arrived.
    /// </summary>
    protected Paged<TRow>? Listed { get; private set; }

    /// <summary>True while a page is being fetched.</summary>
    protected bool Loading { get; private set; } = true;

    /// <summary>The view on screen, or being loaded onto it.</summary>
    protected TView View => _view.Current ?? ViewFromAddress();

    /// <summary>The column the table opens sorted by.</summary>
    protected abstract TColumn DefaultSort { get; }

    /// <summary>Whether that default column opens descending - newest first, say.</summary>
    protected virtual bool DefaultDescending => false;

    /// <summary>The columns this screen draws a sortable heading for. Any other is ignored in an address.</summary>
    protected abstract IReadOnlyCollection<TColumn> Sortable { get; }

    /// <summary>
    /// False while the screen is not ready to list anything yet - an anonymous circuit, a role that
    /// reads no table. The default is always ready.
    /// </summary>
    protected virtual bool ReadyToList => true;

    /// <summary>
    /// The view an address describes. The page, size and sort have already been read; the screen
    /// adds its own filters, read from its own query parameters.
    /// </summary>
    protected abstract TView Compose(int offset, int limit, TColumn sort, bool descending);

    /// <summary>The page, size and sort a view holds.</summary>
    protected abstract (int Offset, int Limit, TColumn Sort, bool Descending) Paging(TView view);

    /// <summary>The same view at a different page, size or sort.</summary>
    protected abstract TView Repaged(TView view, int offset, int limit, TColumn sort, bool descending);

    /// <summary>The screen's filters, as address parameters. Null for a value at its default.</summary>
    protected virtual void AddFilters(TView view, IDictionary<string, object?> address)
    {
    }

    /// <summary>A view arrived from the address: put its filters back into the screen's boxes.</summary>
    protected virtual void Adopt(TView view)
    {
    }

    /// <summary>A page arrived and is now the one on screen.</summary>
    protected virtual void OnListed(Paged<TRow> page)
    {
    }

    /// <summary>Asks the service for the view. Null when the read was refused and the refusal is already on screen.</summary>
    protected abstract Task<Paged<TRow>?> FetchAsync(TView view);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (!ReadyToList)
        {
            // Nothing to list, so nothing is loading: a screen that draws no table for this caller
            // must not sit in a loading state waiting for a page nobody asked for.
            Loading = false;

            return;
        }

        // Back, Forward, a link or a reload is adopted and loaded; this table's own write coming
        // back through the router is recognised and left alone.
        var view = ViewFromAddress();

        if (!_view.Arrived(view))
        {
            return;
        }

        Adopt(view);

        // A view somebody navigated to is a history entry of its own, so the next keystroke must
        // push a new one rather than replace the entry they went back to.
        _typing = false;

        await LoadAsync();
    }

    /// <summary>Loads the view on screen again - after a write, or when something else changed it.</summary>
    protected async Task LoadAsync()
    {
        var view = View;
        var version = ++_version;

        Loading = true;

        // Rendered before the await, or the flag is set and cleared inside one synchronous stretch
        // and the table's loading row never reaches the browser on a refetch.
        StateHasChanged();

        try
        {
            var page = await FetchAsync(view);

            if (version != _version || page is null)
            {
                return;
            }

            // A write that emptied the page on screen - the only row on the last page, deleted -
            // falls back to the last page that still exists rather than showing an empty table over
            // a list that is not empty.
            if (TableQuery.FallbackPage(page) is { } last)
            {
                var (_, limit, sort, descending) = Paging(view);

                // A correction rather than a step: it replaces the entry for the page that is gone.
                _typing = true;

                await MoveAsync(Repaged(view, TableQuery.Offset(last, limit), limit, sort, descending), replace: true);

                // The correction was not a keystroke: the next one starts a run of its own.
                _typing = false;

                return;
            }

            Listed = page;

            OnListed(page);
        }
        finally
        {
            if (version == _version)
            {
                Loading = false;
            }
        }
    }

    /// <summary>
    /// Moves the table to a new view: writes it to the address and loads it. A history entry,
    /// except when <paramref name="replace"/> - which is for typing in a search box.
    /// </summary>
    protected async Task MoveAsync(TView next, bool replace)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (Equals(next, _view.Current))
        {
            return;
        }

        _view.Moved(next);

        // The first keystroke of a run is a history entry of its own and the rest replace it, so
        // Back from a typed search lands on the view before the typing started - not on whatever
        // the first keystroke happened to replace.
        TableQuery.Navigate(Navigation, AddressOf(next), replace && _typing);

        _typing = replace;

        await LoadAsync();
    }

    /// <summary>
    /// The view with its filters changed and the page back at one: a new filter is a new list, and
    /// the page the reader was on in the old one means nothing in it.
    /// </summary>
    protected TView FirstPage(TView view)
    {
        var (_, limit, sort, descending) = Paging(view);

        return Repaged(view, 0, limit, sort, descending);
    }

    /// <summary>The pager chose a page.</summary>
    protected Task GoToPageAsync(int page)
    {
        var (_, limit, sort, descending) = Paging(View);

        return MoveAsync(Repaged(View, TableQuery.Offset(page, limit), limit, sort, descending), replace: false);
    }

    /// <summary>The pager chose a page size. Back to page one with it.</summary>
    protected Task ResizeAsync(int size)
    {
        var (_, _, sort, descending) = Paging(View);

        return MoveAsync(Repaged(View, 0, size, sort, descending), replace: false);
    }

    /// <summary>
    /// A heading was pressed: the same column again reverses it, another column starts ascending.
    /// Back to page one either way.
    /// </summary>
    protected Task SortByAsync(TColumn column)
    {
        var (_, limit, sort, descending) = Paging(View);

        var reversed = EqualityComparer<TColumn>.Default.Equals(sort, column) && !descending;

        return MoveAsync(Repaged(View, 0, limit, column, reversed), replace: false);
    }

    /// <summary>The heading's sort action, bound as a delegate so no lambda has to live in markup.</summary>
    protected Func<Task> SortHandler(TColumn column) => () => SortByAsync(column);

    /// <summary>The heading's <c>aria-sort</c>.</summary>
    protected string SortState(TColumn column)
    {
        var (_, _, sort, descending) = Paging(View);

        return SortHeading.State(EqualityComparer<TColumn>.Default.Equals(sort, column), descending);
    }

    /// <summary>The heading's glyph.</summary>
    protected IconName SortGlyph(TColumn column)
    {
        var (_, _, sort, descending) = Paging(View);

        return SortHeading.Glyph(EqualityComparer<TColumn>.Default.Equals(sort, column), descending);
    }

    /// <summary>
    /// The direction an address that says none means for a column: the screen's own for its default
    /// column, ascending for every other - which is also the direction a heading starts in.
    /// </summary>
    private bool DirectionByDefault(TColumn column) =>
        EqualityComparer<TColumn>.Default.Equals(column, DefaultSort) && DefaultDescending;

    private TView ViewFromAddress()
    {
        var size = TableQuery.Size(SizeParameter);
        var sort = TableQuery.Sort(SortParameter, DefaultSort, Sortable);

        return Compose(
            TableQuery.Offset(TableQuery.Page(PageParameter), size),
            size,
            sort,
            TableQuery.Direction(DescendingParameter, DirectionByDefault(sort)));
    }

    private Dictionary<string, object?> AddressOf(TView view)
    {
        var (offset, limit, sort, descending) = Paging(view);

        var address = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TableQuery.PageName] = TableQuery.PageValue(TableQuery.PageOf(offset, limit)),
            [TableQuery.SizeName] = TableQuery.SizeValue(limit),
            [TableQuery.SortName] = TableQuery.SortValue(sort, DefaultSort),
            [TableQuery.DescendingName] = TableQuery.DirectionValue(descending, DirectionByDefault(sort)),
        };

        AddFilters(view, address);

        return address;
    }
}
