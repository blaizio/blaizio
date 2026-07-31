using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Blaizio.Docs.E2E;

/// <summary>
/// The per-skin visual matrix (TEST-01): every skin, LTR and RTL, light and dark, screenshotted on
/// component pages picked for having burned us before (the RTL switch thumb rendered off-track
/// while every bUnit class assertion passed - only pixels catch that class of defect). One test per
/// page; the 32 skin/dir/theme combos fan out over concurrent browser contexts (Chromium handles
/// them happily; the cap keeps memory sane), so wall-clock is a fraction of a serial sweep.
/// Baselines are machine-local (gitignored): the first run records, later runs compare with a small
/// per-pixel tolerance and write a *.actual.png next to a failing baseline. BLAIZIO_E2E_UPDATE=1
/// re-records after an intentional visual change.
/// </summary>
[Collection("docs-e2e")]
public sealed class VisualRegressionTests(DocsServerFixture fx)
{
    // Keep in step with PresetCode.Styles (src/Blaizio.Cli.Core/Styling/PresetCode.cs), the
    // canonical skin order - not referenced directly because pulling Cli.Core into a Playwright
    // project buys one string list. A new skin must be added here to enter the matrix.
    private static readonly string[] Skins = ["wisp", "spark", "glow", "flint", "forge", "ember", "ash", "aura"];
    private static readonly string[] Dirs = ["ltr", "rtl"];
    private static readonly string[] Themes = ["light", "dark"];

    private const int MaxConcurrentShots = 6;

    // Antialiasing wiggle: a channel may drift a little; a pixel counts as different only past
    // this, and the run fails only when enough pixels differ (spread over the full page).
    private const int ChannelTolerance = 24;
    private const double MaxDifferentPixelRatio = 0.0025;

    // Pages with dense, state-heavy visuals: the switch page IS the RTL-01 regression fixture.
    [E2ETheory]
    [InlineData("docs/components/switch")]
    [InlineData("docs/components/button")]
    [InlineData("docs/components/tabs")]
    public async Task Every_skin_renders_like_its_baseline(string route)
    {
        var gate = new SemaphoreSlim(MaxConcurrentShots);
        var failures = (await Task.WhenAll(
            from skin in Skins
            from dir in Dirs
            from theme in Themes
            select ShootAsync(route, skin, dir, theme, gate)))
            .Where(f => f is not null)
            .ToList();

        Assert.True(failures.Count == 0, $"Visual drift on /{route}:\n{string.Join('\n', failures)}");
    }

    /// <summary>One combo: navigate, freeze, shoot, then record or compare. Returns a failure
    /// description, or null when the combo matches (or was just recorded).</summary>
    private async Task<string?> ShootAsync(string route, string skin, string dir, string theme, SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            await using var context = await fx.NewContextAsync(style: skin, dir: dir, theme: theme);
            var page = await DocsServerFixture.OpenAsync(context, route);

            // boot.js applies the persisted dir pre-paint, but the LAYOUT's direction cascade
            // seeds ltr-first and flips only after the header's first OnAfterRender reads the
            // persisted value - shoot before that flip and an rtl combo captures an ltr page.
            // Wait until the rendered content's computed direction matches the request.
            await page.WaitForFunctionAsync(
                $"() => getComputedStyle(document.querySelector('main') ?? document.body).direction === '{dir}'");

            // Freeze everything that moves so the shot is deterministic: the style tag kills
            // animations/transitions/caret (ReducedMotion and the screenshot option don't cover
            // the caret), then two frames let layout and fonts settle.
            await page.AddStyleTagAsync(new()
            {
                Content = "*, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }",
            });
            // Order matters: webfonts (the RTL demos' Arabic/Hebrew faces) load LAZILY, only once
            // text using them has rendered - so flush two frames first to kick those requests off,
            // THEN await fonts.ready, then settle two more frames for the post-swap relayout.
            await page.EvaluateAsync(
                @"async () => {
                    const frames = () => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                    await frames();
                    await document.fonts.ready;
                    await frames();
                }");

            var shot = await page.ScreenshotAsync(new() { FullPage = true, Animations = ScreenshotAnimations.Disabled });

            var dirPath = Path.Combine(ProjectDir, "Screenshots", route.Replace('/', '_'));
            Directory.CreateDirectory(dirPath);
            var baselinePath = Path.Combine(dirPath, $"{skin}-{dir}-{theme}.png");

            if (E2E.UpdateBaselines || !File.Exists(baselinePath))
            {
                await File.WriteAllBytesAsync(baselinePath, shot);
                return null; // recorded - nothing to compare against yet
            }

            return await CompareAsync(baselinePath, shot, $"{skin}/{dir}/{theme}");
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<string?> CompareAsync(string baselinePath, byte[] shot, string combo)
    {
        using var baseline = await Image.LoadAsync<Rgba32>(baselinePath);
        using var actual = Image.Load<Rgba32>(shot);

        var actualPath = Path.ChangeExtension(baselinePath, ".actual.png");
        if (baseline.Width != actual.Width || baseline.Height != actual.Height)
        {
            await File.WriteAllBytesAsync(actualPath, shot);
            return $"{combo}: size {baseline.Width}x{baseline.Height} -> {actual.Width}x{actual.Height} (see {actualPath})";
        }

        // Row spans + early exit: past the failure budget the verdict is decided - stop counting.
        var budget = (long)(MaxDifferentPixelRatio * baseline.Width * baseline.Height);
        long different = 0;
        baseline.ProcessPixelRows(actual, (baseRows, actualRows) =>
        {
            for (var y = 0; y < baseRows.Height && different <= budget; y++)
            {
                var b = baseRows.GetRowSpan(y);
                var a = actualRows.GetRowSpan(y);
                for (var x = 0; x < b.Length; x++)
                {
                    if (Math.Abs(b[x].R - a[x].R) > ChannelTolerance
                        || Math.Abs(b[x].G - a[x].G) > ChannelTolerance
                        || Math.Abs(b[x].B - a[x].B) > ChannelTolerance)
                        different++;
                }
            }
        });

        if (different > budget)
        {
            await File.WriteAllBytesAsync(actualPath, shot);
            return $"{combo}: over {MaxDifferentPixelRatio:P2} of pixels differ. Diff against {actualPath}; " +
                   "BLAIZIO_E2E_UPDATE=1 re-records if the change is intentional.";
        }

        File.Delete(actualPath); // clean up a stale failure artifact from an earlier run
        return null;
    }

    private static string ProjectDir => Path.Combine(E2E.RepoRoot, "tests", "Blaizio.Docs.E2E");
}
