using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// Reloading a provider-fed BzDataTable. The table deduplicates identical requests, so once a
/// page was loaded nothing could make it ask again: rows changed at the source stayed stale until
/// the user sorted, searched or paged. And a page the source had shrunk away from showed
/// "No results." under a pager with no current page.
/// </summary>
public sealed class DataTableRefreshTests : BunitContext
{
    public DataTableRefreshTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private static List<Person> People(int count, string name = "Person") =>
        [.. Enumerable.Range(1, count).Select(i => new Person { Id = i, Name = $"{name} {i}" })];

    private static DataTableItemsProvider<Person> From(Func<List<Person>> source, List<int> starts) => request =>
    {
        starts.Add(request.StartIndex);
        var all = source();
        return ValueTask.FromResult(new DataTableResult<Person>(
            [.. all.Skip(request.StartIndex).Take(request.Count)], all.Count));
    };

    [Fact]
    public async Task RefreshDataAsync_asks_the_provider_for_the_same_page_again()
    {
        var people = People(4);
        var starts = new List<int>();
        var cut = Render<RefreshGrid>(p => p.Add(x => x.Provider, From(() => people, starts)));
        cut.WaitForAssertion(() => Assert.Contains("Person 1", cut.Markup));

        // Renamed at the source. Rendering again asks nothing: the request is the one already answered.
        people = People(4, "Renamed");
        cut.Render();
        Assert.DoesNotContain("Renamed", cut.Markup);
        Assert.Equal([0], starts);

        await cut.InvokeAsync(() => cut.Instance.Table!.RefreshDataAsync());

        cut.WaitForAssertion(() => Assert.Contains("Renamed 1", cut.Markup));
        Assert.Equal([0, 0], starts);
    }

    [Fact]
    public async Task A_reload_that_finds_the_page_gone_moves_to_the_last_page()
    {
        var people = People(6); // three pages of two
        var starts = new List<int>();
        var cut = Render<RefreshGrid>(p => p
            .Add(x => x.Provider, From(() => people, starts))
            .Add(x => x.StartPage, 3));
        cut.WaitForAssertion(() => Assert.Contains("Person 5", cut.Markup));

        // Three rows deleted at the source: two pages are left, and page three is not one of them.
        people = People(3);
        await cut.InvokeAsync(() => cut.Instance.Table!.RefreshDataAsync());

        cut.WaitForAssertion(() => Assert.Contains("Person 3", cut.Markup));
        Assert.Equal([2], cut.Instance.Pages);
        Assert.Equal([4, 4, 2], starts); // page three, page three again, then the last page
    }

    [Fact]
    public async Task A_table_that_keeps_its_own_page_moves_to_the_last_page_too()
    {
        var people = People(6);
        var starts = new List<int>();
        var cut = Render<BzDataTable<Person>>(p => p
            .Add(x => x.ItemsProvider, From(() => people, starts))
            .Add(x => x.PageSize, 2)
            .Add(x => x.DefaultPage, 3));
        cut.WaitForAssertion(() => Assert.Equal([4], starts));

        people = People(3);
        await cut.InvokeAsync(() => cut.Instance.RefreshDataAsync());

        cut.WaitForAssertion(() => Assert.Equal([4, 4, 2], starts));
    }

    [Fact]
    public async Task RefreshDataAsync_does_nothing_for_a_table_that_owns_its_rows()
    {
        var cut = Render<BzDataTable<Person>>(p => p.Add(x => x.Items, Person.Sample()));

        await cut.InvokeAsync(() => cut.Instance.RefreshDataAsync());
    }
}
