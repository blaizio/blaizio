using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// Every key route renders (its ready selector present via the live runtime) with a clean console:
/// no page errors and no Blaizio dev-aid warnings - the docs app dogfoods the components, so an
/// unnamed-composite warning here means either a demo lost its label or a component lost its typed
/// name parameter (locks in audit batch 9).
/// </summary>
[Collection("docs-e2e")]
public sealed class RoutingTests(DocsServerFixture fx)
{
    public static readonly TheoryData<string> Routes =
    [
        "",
        "docs/components/accordion",
        "docs/components/tabs",
        "docs/components/switch",
        "docs/components/select",
        "docs/components/carousel",
        "docs/components/color-picker",
        "docs/components/tree",
        "docs/components/resizable",
        "docs/components/menubar",
        "themes",
        "community",
        "docs/registry",
        "docs/registry/registry-item-json",
    ];

    [E2ETheory]
    [MemberData(nameof(Routes))]
    public async Task Route_renders_with_clean_console(string route)
    {
        await using var context = await fx.NewContextAsync();
        var page = await context.NewPageAsync();

        // Listeners attach BEFORE navigation so nothing said during boot is missed.
        var warnings = new List<string>();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "warning" && message.Text.Contains("Blaizio")) warnings.Add(message.Text);
            if (message.Type == "error") errors.Add(message.Text);
        };
        page.PageError += (_, error) => errors.Add(error);

        await DocsServerFixture.OpenAsync(page, route);
        await page.WaitForTimeoutAsync(500); // settle window for late console output from the runtime

        Assert.Empty(warnings);
        Assert.Empty(errors);
    }
}
