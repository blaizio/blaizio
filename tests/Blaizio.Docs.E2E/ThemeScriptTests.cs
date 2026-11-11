using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The body font knob of the theme script, driven through the real module in a real page. Its
/// marker class is <c>font-{name}</c>, a prefix the app's own classes on <c>&lt;html&gt;</c>
/// share, so the knob must touch only the class it added.
/// </summary>
[Collection("docs-e2e")]
public sealed class ThemeScriptTests(DocsServerFixture fx)
{
    private static HashSet<string> Classes(string className) =>
        [.. className.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    [E2EFact]
    public async Task Font_knob_touches_only_the_class_it_added()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/button");

        // The app's own font classes: a built-in utility, one from its own @theme, and one with
        // the important mark. The last two used to be read as the selected font and removed.
        var steps = await page.EvaluateAsync<string[]>(
            """
            async () => {
                const html = document.documentElement;
                html.classList.add('font-sans', 'font-display', 'font-sans!');
                const theme = await import(new URL('_content/blaizio.base/dist/theme.js', document.baseURI).href);
                const before = theme.getFont();
                theme.setFont('inter');
                const afterSet = html.className;
                theme.setFont('geist');
                const afterSwitch = html.className;
                const stored = theme.getFont();
                theme.setFont('default');
                const afterDefault = html.className;
                theme.setFont('inter'); // left stored for the reload below
                return [before, afterSet, afterSwitch, stored, afterDefault];
            }
            """);

        Assert.Equal("default", steps[0]);

        var afterSet = Classes(steps[1]);
        Assert.Superset(new HashSet<string> { "font-inter", "font-sans", "font-display", "font-sans!" }, afterSet);

        var afterSwitch = Classes(steps[2]);
        Assert.Contains("font-geist", afterSwitch);
        Assert.DoesNotContain("font-inter", afterSwitch);
        Assert.Superset(new HashSet<string> { "font-sans", "font-display", "font-sans!" }, afterSwitch);
        Assert.Equal("geist", steps[3]);

        var afterDefault = Classes(steps[4]);
        Assert.DoesNotContain("font-geist", afterDefault);
        Assert.Superset(new HashSet<string> { "font-sans", "font-display", "font-sans!" }, afterDefault);

        // The pre-paint script applies the stored font on the next load, and the knob still
        // knows which class to take away.
        await page.ReloadAsync();
        await page.Locator("h1").First.WaitForAsync(new() { Timeout = 30_000 });
        Assert.Contains("font-inter", Classes(await page.EvaluateAsync<string>("() => document.documentElement.className")));

        var cleared = await page.EvaluateAsync<string>(
            """
            async () => {
                const theme = await import(new URL('_content/blaizio.base/dist/theme.js', document.baseURI).href);
                theme.setFont('default');
                return document.documentElement.className;
            }
            """);
        Assert.DoesNotContain("font-inter", Classes(cleared));
    }

    [E2EFact]
    public async Task A_name_with_whitespace_is_refused_before_it_is_stored()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/button");

        var result = await page.EvaluateAsync<string[]>(
            """
            async () => {
                const theme = await import(new URL('_content/blaizio.base/dist/theme.js', document.baseURI).href);
                let threw = 'no';
                try { theme.setFont('two words'); } catch { threw = 'yes'; }
                return [threw, String(localStorage.getItem('blaizio-font'))];
            }
            """);

        Assert.Equal("yes", result[0]);
        Assert.Equal("null", result[1]);
    }
}
