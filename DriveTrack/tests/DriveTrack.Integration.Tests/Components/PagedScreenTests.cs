using DriveTrack.Application.Common;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// The behaviour every paged table inherits from <see cref="PagedScreen{TView, TRow, TColumn}"/>,
/// driven directly.
/// <para>
/// Static rendering presses nothing, so a heading, the pager or a search box can only be exercised
/// by calling the handler it is bound to. A probe screen does that on the renderer's own dispatcher
/// and records every view it is asked to fetch and every address it writes - which is what the
/// design's rules are about: a new sort, filter or page size returns to page one; a page that came
/// back empty steps back to the last one that exists; an address reproduces the view it names, and
/// one nobody can parse falls back rather than failing.
/// </para>
/// </summary>
public class PagedScreenTests
{
    [Fact]
    public async Task A_new_sort_returns_to_page_one_and_the_same_heading_twice_reverses_it()
    {
        var backend = new ProbeBackend();

        await RenderAsync("probe?page=3", backend, async screen =>
        {
            await screen.SortBy(ProbeColumn.Name);
            await screen.SortBy(ProbeColumn.Name);

            Assert.Equal("descending", screen.State(ProbeColumn.Name));
            Assert.Equal("none", screen.State(ProbeColumn.Created));
        });

        Assert.Equal(
            [
                new ProbeView(20, 10, ProbeColumn.Created, true, null),
                new ProbeView(0, 10, ProbeColumn.Name, false, null),
                new ProbeView(0, 10, ProbeColumn.Name, true, null),
            ],
            backend.Asked);

        // A sort is a place Back should stop at, so both presses are history entries.
        Assert.Equal([false, false], backend.Navigation.Replaced);
        Assert.DoesNotContain("page=", backend.Navigation.Visited[^1], StringComparison.Ordinal);
        Assert.Contains("sort=Name", backend.Navigation.Visited[^1], StringComparison.Ordinal);
        Assert.Contains("desc=true", backend.Navigation.Visited[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_search_returns_to_page_one_and_only_its_first_keystroke_is_a_history_entry()
    {
        var backend = new ProbeBackend();

        await RenderAsync("probe?page=4", backend, async screen =>
        {
            await screen.Search("k");
            await screen.Search("ko");
            await screen.Search("kov");
        });

        Assert.Equal(30, backend.Asked[0].Offset);
        Assert.All(backend.Asked.Skip(1), view => Assert.Equal(0, view.Offset));
        Assert.Equal("kov", backend.Asked[^1].Search);

        // Back from the typed search lands on page four, not on a half-typed term.
        Assert.Equal([false, true, true], backend.Navigation.Replaced);
        Assert.Contains("q=kov", backend.Navigation.Visited[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_page_size_returns_to_page_one_and_a_page_moves_by_its_own_size()
    {
        var backend = new ProbeBackend { Total = 200 };

        await RenderAsync("probe?page=6", backend, async screen =>
        {
            await screen.Resize(25);
            await screen.Page(3);
        });

        Assert.Equal(
            [
                new ProbeView(50, 10, ProbeColumn.Created, true, null),
                new ProbeView(0, 25, ProbeColumn.Created, true, null),
                new ProbeView(50, 25, ProbeColumn.Created, true, null),
            ],
            backend.Asked);
        Assert.Contains("size=25", backend.Navigation.Visited[^1], StringComparison.Ordinal);
        Assert.Contains("page=3", backend.Navigation.Visited[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_that_came_back_empty_steps_back_to_the_last_page_that_exists()
    {
        // The only row on page three was deleted: twelve rows remain, so page three is past the
        // end and page two is the last one there is.
        var backend = new ProbeBackend { Total = 12 };

        await RenderAsync("probe?page=3", backend, _ => Task.CompletedTask);

        Assert.Equal([20, 10], backend.Asked.Select(view => view.Offset));

        // A correction, not a step: it replaces the entry for the page that is gone.
        Assert.Equal([true], backend.Navigation.Replaced);
        Assert.Contains("page=2", backend.Navigation.Visited[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_address_reproduces_the_view_it_names()
    {
        var backend = new ProbeBackend();

        await RenderAsync("probe?page=3&size=25&sort=Name&desc=true&q=kov", backend, _ => Task.CompletedTask);

        Assert.Equal([new ProbeView(50, 25, ProbeColumn.Name, true, "kov")], backend.Asked);
        Assert.Empty(backend.Navigation.Visited);
    }

    [Theory]
    [InlineData("probe?page=abc&size=7&sort=bogus&desc=maybe")]
    [InlineData("probe?page=-2&size=1000&sort=3")]
    [InlineData("probe?page=0&sort=Hidden")]
    public async Task An_address_nobody_can_parse_falls_back_to_the_default_view(string address)
    {
        var backend = new ProbeBackend();

        await RenderAsync(address, backend, _ => Task.CompletedTask);

        Assert.Equal([new ProbeView(0, 10, ProbeColumn.Created, true, null)], backend.Asked);
    }

    [Fact]
    public async Task The_first_keystroke_after_an_empty_page_s_fallback_is_a_history_entry_of_its_own()
    {
        // The fallback replaces the entry for the page that is gone; the search typed afterwards is
        // a new place Back should stop at, not a second correction of the first.
        var backend = new ProbeBackend { Total = 12 };

        await RenderAsync("probe?page=3", backend, async screen =>
        {
            await screen.Search("k");
            await screen.Search("ko");
        });

        Assert.Equal([true, false, true], backend.Navigation.Replaced);
    }

    [Fact]
    public async Task The_first_keystroke_after_an_address_is_adopted_is_a_history_entry_of_its_own()
    {
        // Typing, then Back to where the typing started, then typing again: the second run must not
        // replace the entry the reader went back to.
        var backend = new ProbeBackend();

        await RenderAsync("probe?page=2", backend, async screen =>
        {
            await screen.Search("k");
            await screen.Search("ko");

            await backend.Navigation.ArriveAsync("http://localhost/probe?page=2");

            Assert.Null(screen.Current.Search);
            Assert.Equal(10, screen.Current.Offset);

            await screen.Search("x");
        });

        Assert.Equal([false, true, false], backend.Navigation.Replaced);
        Assert.Equal(new ProbeView(10, 10, ProbeColumn.Created, true, null), backend.Asked[3]);
    }

    [Fact]
    public async Task A_load_overtaken_by_a_newer_one_does_not_overwrite_the_newer_page()
    {
        // Two presses on the pager before either page has arrived, answered out of order: the page
        // on screen is the one asked for last, whichever answer came back last.
        var backend = new ProbeBackend { Total = 200 };

        await RenderAsync("probe", backend, async screen =>
        {
            backend.Defer = true;

            var older = screen.Page(2);
            var newer = screen.Page(3);

            backend.Release(new ProbeView(20, 10, ProbeColumn.Created, true, null));
            await newer;

            backend.Release(new ProbeView(10, 10, ProbeColumn.Created, true, null));
            await older;

            Assert.Equal(20, screen.Shown!.Offset);
            Assert.False(screen.IsLoading);
        });
    }

    [Fact]
    public async Task The_table_s_own_address_arriving_late_does_not_put_older_text_back_in_the_box()
    {
        // The router delivers a written address after the reader has typed on: the first keystroke's
        // address arrives once the box already says "kov". Adopting it would revert the box under
        // their fingers; it is recognised as the table's own write and left alone.
        var backend = new ProbeBackend();

        await RenderAsync("probe", backend, async screen =>
        {
            await screen.Search("k");
            await screen.Search("ko");
            await screen.Search("kov");

            var asked = backend.Asked.Count;

            await backend.Navigation.ArriveAsync(backend.Navigation.Visited[0]);

            Assert.Equal("kov", screen.Current.Search);
            Assert.Equal(asked, backend.Asked.Count);
        });
    }

    [Theory]
    [InlineData(3, 1, 0, null)] // a page with rows on it stays
    [InlineData(0, 0, 20, null)] // an empty list has nowhere to step back to
    [InlineData(0, 12, 0, null)] // the first page never moves
    [InlineData(0, 12, 20, 2)]
    [InlineData(0, 30, 90, 3)]
    public void The_fallback_is_the_last_page_and_only_for_an_empty_page_of_a_list_that_is_not(
        int items,
        int total,
        int offset,
        int? expected)
    {
        var page = new Paged<int>([.. Enumerable.Range(1, items)], total, offset, 10);

        Assert.Equal(expected, TableQuery.FallbackPage(page));
    }

    private static Task<string> RenderAsync(string address, ProbeBackend backend, Func<ProbeScreen, Task> act)
    {
        backend.Navigation = new ProbeNavigation(address);

        return ComponentRenderer.RenderThenAsync<ProbeScreen>(
            services =>
            {
                services.AddSupplyValueFromQueryProvider();
                services.AddSingleton<NavigationManager>(backend.Navigation);
                services.AddSingleton(backend);
            },
            _ => act(backend.Screen!));
    }

    internal enum ProbeColumn
    {
        Name,
        Created,

        // A column the list has and the screen draws no heading for.
        Hidden,
    }

    internal sealed record ProbeView(int Offset, int Limit, ProbeColumn Sort, bool Descending, string? Search);

    /// <summary>What the probe answers, and the record of what it was asked.</summary>
    internal sealed class ProbeBackend
    {
        public int Total { get; init; } = 86;

        public List<ProbeView> Asked { get; } = [];

        public ProbeNavigation Navigation { get; set; } = new("probe");

        public ProbeScreen? Screen { get; set; }

        /// <summary>When set, a fetch waits until the test releases the view it asked for.</summary>
        public bool Defer { get; set; }

        private readonly Dictionary<ProbeView, TaskCompletionSource<Paged<int>?>> _pending = [];

        public Task<Paged<int>?> FetchAsync(ProbeView view)
        {
            var page = Answer(view);

            if (!Defer)
            {
                return Task.FromResult<Paged<int>?>(page);
            }

            var pending = new TaskCompletionSource<Paged<int>?>();

            _pending[view] = pending;

            return pending.Task;
        }

        /// <summary>Lets the deferred fetch for <paramref name="view"/> answer.</summary>
        public void Release(ProbeView view) => _pending[view].SetResult(Answer(view, record: false));

        public Paged<int> Answer(ProbeView view, bool record = true)
        {
            if (record)
            {
                Asked.Add(view);
            }

            var count = Math.Clamp(Total - view.Offset, 0, view.Limit);

            return new Paged<int>([.. Enumerable.Range(view.Offset + 1, count)], Total, view.Offset, view.Limit);
        }
    }

    /// <summary>A navigation manager at a given address that records where it was sent, and how.</summary>
    internal sealed class ProbeNavigation : NavigationManager
    {
        public ProbeNavigation(string path) => Initialize("http://localhost/", "http://localhost/" + path);

        public List<string> Visited { get; } = [];

        public List<bool> Replaced { get; } = [];

        /// <summary>
        /// The browser reports the address changed - Back, Forward, a link, or the router catching
        /// up with a write - as the real navigation manager would, and the screen re-reads it.
        /// </summary>
        public async Task ArriveAsync(string uri)
        {
            Uri = uri;

            NotifyLocationChanged(isInterceptedLink: false);

            // The query-string supplier re-renders the screen on the same dispatcher; let it run.
            await Task.Yield();
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Visited.Add(uri);
            Replaced.Add(options.ReplaceHistoryEntry);
        }
    }

    /// <summary>A paged screen with no markup, whose handlers a test can reach.</summary>
    internal sealed class ProbeScreen : PagedScreen<ProbeView, int, ProbeColumn>
    {
        [Inject]
        private ProbeBackend Backend { get; set; } = default!;

        protected override ProbeColumn DefaultSort => ProbeColumn.Created;

        protected override bool DefaultDescending => true;

        protected override IReadOnlyCollection<ProbeColumn> Sortable => [ProbeColumn.Name, ProbeColumn.Created];

        public Task SortBy(ProbeColumn column) => SortByAsync(column);

        public Task Page(int page) => GoToPageAsync(page);

        public Task Resize(int size) => ResizeAsync(size);

        public Task Search(string text) => MoveAsync(FirstPage(View with { Search = text }), replace: true);

        public string State(ProbeColumn column) => SortState(column);

        public ProbeView Current => View;

        public Paged<int>? Shown => Listed;

        public bool IsLoading => Loading;

        protected override void OnInitialized()
        {
            base.OnInitialized();

            Backend.Screen = this;
        }

        protected override ProbeView Compose(int offset, int limit, ProbeColumn sort, bool descending) =>
            new(offset, limit, sort, descending, TableQuery.Text(SearchParameter));

        protected override (int Offset, int Limit, ProbeColumn Sort, bool Descending) Paging(ProbeView view) =>
            (view.Offset, view.Limit, view.Sort, view.Descending);

        protected override ProbeView Repaged(ProbeView view, int offset, int limit, ProbeColumn sort, bool descending) =>
            view with { Offset = offset, Limit = limit, Sort = sort, Descending = descending };

        protected override void AddFilters(ProbeView view, IDictionary<string, object?> address) =>
            address[TableQuery.SearchName] = view.Search;

        protected override Task<Paged<int>?> FetchAsync(ProbeView view) => Backend.FetchAsync(view);
    }
}
