using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// A panel that is rendered later registers last, whatever its place in the markup. The group asks
/// the script where its panels and handles sit in the document and follows that order; only a real
/// page can answer. Before this, a first panel that was hidden and shown again came back as the
/// last one: the handle next to it had no neighbour and a drag on it did nothing.
/// </summary>
[Collection("docs-e2e")]
public sealed class ResizableOrderTests(DocsServerFixture fx)
{
    private static async Task<double[]> SizesAsync(ILocator group) =>
        [.. (await group.Locator(":scope > [data-slot=resizable-panel]").EvaluateAllAsync<string[]>(
                "panels => panels.map(panel => String(panel.getBoundingClientRect().width))"))
            .Select(width => double.Parse(width, CultureInfo.InvariantCulture))];

    private static async Task DragAsync(IPage page, ILocator handle, double by)
    {
        var box = (await handle.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await page.Mouse.MoveAsync((float)x, (float)y);
        await page.Mouse.DownAsync();
        for (var step = 1; step <= 8; step++) await page.Mouse.MoveAsync((float)(x + by * step / 8), (float)y);
        await page.Mouse.UpAsync();
    }

    [E2EFact]
    public async Task A_first_panel_hidden_and_shown_again_is_first_again_and_its_handle_moves_its_own_pair()
    {
        await using var context = await fx.NewContextAsync();
        var page = await DocsServerFixture.OpenAsync(context, "docs/components/resizable");

        var filesHandle = page.GetByRole(AriaRole.Separator, new() { Name = "Resize files" });
        var group = page.Locator("[data-slot=resizable-panel-group]", new() { Has = page.GetByRole(AriaRole.Separator, new() { Name = "Resize preview" }) });
        var panels = group.Locator(":scope > [data-slot=resizable-panel]");
        await group.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(panels).ToHaveCountAsync(3);

        await page.GetByRole(AriaRole.Button, new() { Name = "Hide files" }).ClickAsync();
        await Assertions.Expect(panels).ToHaveCountAsync(2);

        await page.GetByRole(AriaRole.Button, new() { Name = "Show files" }).ClickAsync();
        await Assertions.Expect(panels).ToHaveCountAsync(3);
        await Assertions.Expect(panels.First).ToContainTextAsync("Files");
        // The handle that came back is wired a moment after it renders (its script is created on
        // its first render), and the group reads the order after that same render.
        await Assertions.Expect(filesHandle).ToHaveAttributeAsync("aria-valuenow", "25");
        await page.WaitForTimeoutAsync(400);

        // Files, Editor and Preview are back at 25, 50 and 25: nobody's size moved in the reorder.
        var before = await SizesAsync(group);
        var total = before.Sum();
        Assert.InRange(before[0] / total, 0.23, 0.27);
        Assert.InRange(before[1] / total, 0.48, 0.52);
        Assert.InRange(before[2] / total, 0.23, 0.27);

        // Drag the handle between Files and Editor a tenth of the group to the right.
        await DragAsync(page, filesHandle, total * 0.10);

        var after = await SizesAsync(group);
        Assert.InRange((after[0] - before[0]) / total, 0.07, 0.13);    // Files grew
        Assert.InRange((before[1] - after[1]) / total, 0.07, 0.13);    // Editor gave the space
        Assert.InRange(Math.Abs(after[2] - before[2]) / total, 0, 0.01); // Preview did not move

        // And the other handle still moves Editor and Preview, not Files.
        await DragAsync(page, page.GetByRole(AriaRole.Separator, new() { Name = "Resize preview" }), -total * 0.05);

        var last = await SizesAsync(group);
        Assert.InRange(Math.Abs(last[0] - after[0]) / total, 0, 0.01);
        Assert.InRange((after[1] - last[1]) / total, 0.03, 0.07);
        Assert.InRange((last[2] - after[2]) / total, 0.03, 0.07);
    }
}
