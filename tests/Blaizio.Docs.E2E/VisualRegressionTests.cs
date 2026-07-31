using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The per-skin visual matrix (TEST-01): every skin, LTR and RTL, light and dark, screenshotted on
/// component pages picked for having burned us before (the RTL switch thumb rendered off-track
/// while every bUnit class assertion passed - only pixels catch that class of defect). Baselines
/// are machine-local (gitignored): the first run records, later runs compare with a small
/// per-pixel tolerance and write a *.actual.png next to a failing baseline. BLAIZIO_E2E_UPDATE=1
/// re-records after an intentional visual change.
/// </summary>
[Collection("docs-e2e")]
public sealed class VisualRegressionTests(DocsServerFixture fx)
{
    private static readonly string[] Skins = ["wisp", "spark", "glow", "flint", "forge", "ember", "ash", "aura"];
    private static readonly string[] Dirs = ["ltr", "rtl"];
    private static readonly string[] Themes = ["light", "dark"];

    // Pages with dense, state-heavy visuals: the switch page IS the RTL-01 regression fixture.
    private static readonly string[] Pages = ["docs/components/switch", "docs/components/button", "docs/components/tabs"];

    // Antialiasing wiggle: a channel may drift a little; a pixel counts as different only past
    // this, and the run fails only when enough pixels differ (spread over the full page).
    private const int ChannelTolerance = 24;
    private const double MaxDifferentPixelRatio = 0.0025;

    public static TheoryData<string, string, string, string> Matrix()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var page in Pages)
            foreach (var skin in Skins)
                foreach (var dir in Dirs)
                    foreach (var theme in Themes)
                        data.Add(page, skin, dir, theme);
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Skin_renders_like_its_baseline(string route, string skin, string dir, string theme)
    {
        if (!E2E.Enabled) return;

        await using var context = await fx.NewContextAsync(style: skin, dir: dir, theme: theme);
        var page = await DocsServerFixture.OpenAsync(context, route);

        // Freeze everything that moves so the shot is deterministic.
        await page.AddStyleTagAsync(new()
        {
            Content = "*, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }",
        });
        await page.EvaluateAsync("() => document.fonts.ready");
        await page.WaitForTimeoutAsync(250);

        var shot = await page.ScreenshotAsync(new() { FullPage = true, Animations = ScreenshotAnimations.Disabled });

        var slug = route.Replace('/', '_');
        var dirPath = Path.Combine(ProjectDir(), "Screenshots", slug);
        Directory.CreateDirectory(dirPath);
        var baselinePath = Path.Combine(dirPath, $"{skin}-{dir}-{theme}.png");

        if (E2E.UpdateBaselines || !File.Exists(baselinePath))
        {
            await File.WriteAllBytesAsync(baselinePath, shot);
            return; // recorded - nothing to compare against yet
        }

        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var actual = Image.Load<Rgba32>(shot);

        var actualPath = Path.Combine(dirPath, $"{skin}-{dir}-{theme}.actual.png");
        if (baseline.Width != actual.Width || baseline.Height != actual.Height)
        {
            await File.WriteAllBytesAsync(actualPath, shot);
            Assert.Fail($"Size changed for {skin}/{dir}/{theme} on /{route}: " +
                        $"{baseline.Width}x{baseline.Height} -> {actual.Width}x{actual.Height} (see {actualPath})");
        }

        long different = 0;
        for (var y = 0; y < baseline.Height; y++)
        {
            for (var x = 0; x < baseline.Width; x++)
            {
                var b = baseline[x, y];
                var a = actual[x, y];
                if (Math.Abs(b.R - a.R) > ChannelTolerance
                    || Math.Abs(b.G - a.G) > ChannelTolerance
                    || Math.Abs(b.B - a.B) > ChannelTolerance)
                    different++;
            }
        }

        var ratio = (double)different / (baseline.Width * (long)baseline.Height);
        if (ratio > MaxDifferentPixelRatio)
        {
            await File.WriteAllBytesAsync(actualPath, shot);
            Assert.Fail($"Visual drift for {skin}/{dir}/{theme} on /{route}: {ratio:P2} of pixels differ " +
                        $"(threshold {MaxDifferentPixelRatio:P2}). Diff against {actualPath}; " +
                        "BLAIZIO_E2E_UPDATE=1 re-records if the change is intentional.");
        }

        File.Delete(actualPath); // clean up a stale failure artifact from an earlier run
    }

    private static string ProjectDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Blaizio.Docs.E2E.csproj")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Blaizio.Docs.E2E.csproj not found above the test assembly.");
    }
}
