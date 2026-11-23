using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The site starts Blazor itself (js/start.js) with a boot resource loader. A boot file whose
/// first response is an error is asked for once more past the HTTP cache, so one stored error
/// response (a visit while a deploy was rolling out) no longer leaves the browser on the splash.
/// The requests are read from the page's own <c>fetch</c> calls: routing a request turns the HTTP
/// cache off, so the request headers cannot tell the two cache modes apart.
/// </summary>
[Collection("docs-e2e")]
public sealed class BootRecoveryTests(DocsServerFixture fx)
{
    /// <summary>Records the cache mode of every <c>fetch</c> for a file under <c>_framework/</c>,
    /// as "name|mode" with the fingerprint left in the name.</summary>
    private const string RecordBootFetches =
        """
        (() => {
            const calls = (window.__bootFetches = []);
            const original = window.fetch;
            window.fetch = function (input, init) {
                const url = typeof input === 'string' ? input : input.url;
                const at = url.indexOf('/_framework/');
                if (at >= 0) calls.push(url.slice(at + '/_framework/'.length) + '|' + (init?.cache ?? 'default'));
                return original.apply(this, arguments);
            };
        })();
        """;

    private static async Task<List<(string Name, string Cache)>> BootFetchesAsync(IPage page) =>
        [.. (await page.EvaluateAsync<string[]>("() => window.__bootFetches"))
            .Select(call => call.Split('|'))
            .Select(parts => (parts[0], parts[1]))];

    [E2EFact]
    public async Task Boot_file_that_fails_once_is_fetched_again_past_the_cache()
    {
        await using var context = await fx.NewContextAsync();
        await context.AddInitScriptAsync(RecordBootFetches);
        var page = await context.NewPageAsync();

        // The app's own assembly gets, once, what a static host answers for a file it does not
        // have: its not-found page with status 404. The integrity check rejects that body.
        var answered = 0;
        await page.RouteAsync("**/_framework/Blaizio.Docs.*.wasm", async route =>
        {
            if (Interlocked.Increment(ref answered) == 1)
                await route.FulfillAsync(new() { Status = 404, ContentType = "text/html", Body = "<!DOCTYPE html><title>Not found</title>" });
            else
                await route.ContinueAsync();
        });

        // Renders its h1, so the app booted.
        await DocsServerFixture.OpenAsync(page, "");

        var modes = (await BootFetchesAsync(page))
            .Where(call => call.Name.StartsWith("Blaizio.Docs.", StringComparison.Ordinal) && call.Name.EndsWith(".wasm", StringComparison.Ordinal))
            .Select(call => call.Cache)
            .ToArray();
        Assert.Equal(new[] { "force-cache", "reload" }, modes);
        Assert.Equal(2, answered);
    }

    [E2EFact]
    public async Task Boot_that_goes_well_asks_for_each_file_once_from_the_cache()
    {
        await using var context = await fx.NewContextAsync();
        await context.AddInitScriptAsync(RecordBootFetches);
        var page = await DocsServerFixture.OpenAsync(context, "");

        var calls = await BootFetchesAsync(page);
        var assemblies = calls.Where(call => call.Name.EndsWith(".wasm", StringComparison.Ordinal)).ToArray();

        // The loader's first request is the runtime's own: no file twice, none past the cache.
        Assert.NotEmpty(assemblies);
        Assert.All(assemblies, call => Assert.Equal("force-cache", call.Cache));
        Assert.Equal(assemblies.Length, assemblies.Select(call => call.Name).Distinct().Count());
        Assert.DoesNotContain(calls, call => call.Cache == "reload");
    }
}
