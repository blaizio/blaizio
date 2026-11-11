using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// What a provider-fed BzDataTable and BzVirtualizer make of a request that ends badly. Two
/// cases used to be read the wrong way round: a cancellation the provider raised by itself (a
/// client timeout) was taken for "superseded" and left the rows loading for good, and a request
/// that really was superseded took the page down when its client answered the cancel with its
/// own exception type.
/// </summary>
public sealed class ProviderFailureTests : BunitContext
{
    public ProviderFailureTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private static DataTableResult<Person> Page() => new(Person.Sample(), 3);

    [Fact]
    public void A_table_reports_a_cancellation_the_provider_raised_by_itself()
    {
        var calls = 0;
        var cut = Render<ProviderGrid>(p => p.Add(x => x.Provider, _ =>
            ++calls == 1
                ? throw new TaskCanceledException("The request timed out.") // not our token: a client timeout
                : ValueTask.FromResult(Page())));

        cut.WaitForAssertion(() => Assert.Equal("TaskCanceledException", cut.Find("[data-reported]").TextContent));

        // And the request was not left deduplicated: the same page loads once the app recovers.
        cut.InvokeAsync(() => cut.Instance.Boundary!.Recover());
        cut.WaitForAssertion(() => Assert.Contains("Ada", cut.Markup));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task A_table_ignores_whatever_a_superseded_request_throws()
    {
        var first = new TaskCompletionSource<DataTableResult<Person>>();
        var calls = 0;
        DataTableItemsProvider<Person> provider = request =>
            ++calls == 1 ? new ValueTask<DataTableResult<Person>>(first.Task) : ValueTask.FromResult(Page());
        var cut = Render<ProviderGrid>(p => p.Add(x => x.Provider, provider));

        // A different page size is a different request: the first one is cancelled and replaced.
        cut.Render(p => p.Add(x => x.Provider, provider).Add(x => x.PageSize, 10));
        cut.WaitForAssertion(() => Assert.Contains("Ada", cut.Markup));

        // Its client answers the cancel with an exception of its own, not an OperationCanceledException.
        await cut.InvokeAsync(() => first.SetException(new InvalidOperationException("Call cancelled.")));

        Assert.Empty(cut.FindAll("[data-reported]"));
        Assert.Contains("Ada", cut.Markup);
    }

    [Fact]
    public void A_table_still_reports_a_provider_that_fails()
    {
        var cut = Render<ProviderGrid>(p => p.Add(x => x.Provider,
            _ => throw new InvalidOperationException("Server said no.")));

        cut.WaitForAssertion(() => Assert.Equal("InvalidOperationException", cut.Find("[data-reported]").TextContent));
    }

    [Fact]
    public void A_virtualizer_reports_a_cancellation_the_provider_raised_by_itself()
    {
        var cut = Render<ProviderList>(p => p.Add(x => x.Provider,
            _ => throw new TaskCanceledException("The request timed out.")));

        cut.WaitForAssertion(() => Assert.Equal("TaskCanceledException", cut.Find("[data-reported]").TextContent));
    }
}
