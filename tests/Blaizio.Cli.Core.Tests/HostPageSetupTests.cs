using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class HostPageSetupTests
{
    private const string WasmIndex =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>App</title>
        </head>
        <body>
            <div id="app"></div>
        </body>
        </html>
        """;

    [Fact]
    public async Task Wires_a_wasm_index_html()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);

        var result = await new HostPageSetup().EnsureAsync(dir.Path);

        Assert.Equal("wwwroot/index.html", result.HostPath);
        var html = dir.Read("wwwroot/index.html");
        // v3: no style-*/preset-* class - the look ships inlined in the components.
        Assert.DoesNotContain("style-", html);
        Assert.Contains("<link rel=\"stylesheet\" href=\"app.css\" />", html);
        Assert.Contains("<script src=\"_content/blaizio.base/dist/boot.js\"></script>", html);
        // Inserted inside <head>, before its close.
        Assert.True(html.IndexOf("boot.js", StringComparison.Ordinal) < html.IndexOf("</head>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);
        var setup = new HostPageSetup();

        await setup.EnsureAsync(dir.Path);
        var afterFirst = dir.Read("wwwroot/index.html");
        var second = await setup.EnsureAsync(dir.Path);

        Assert.Empty(second.Changes);
        Assert.Equal(afterFirst, dir.Read("wwwroot/index.html"));
    }

    [Fact]
    public async Task Strips_stale_v1_skin_and_preset_classes_and_keeps_the_rest()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex.Replace(
            "<html lang=\"en\">", "<html lang=\"en\" class=\"dark style-aura preset-comet\">"));

        await new HostPageSetup().EnsureAsync(dir.Path);

        var html = dir.Read("wwwroot/index.html");
        Assert.Contains("class=\"dark\"", html); // .dark is the app's - stays
        Assert.DoesNotContain("style-aura", html);
        Assert.DoesNotContain("preset-comet", html);
    }

    [Fact]
    public async Task Never_touches_the_dir_attribute()
    {
        // The config's rtl flag means RTL *support* (logical properties) - it must never turn the
        // page RTL. Page direction is the app's (boot.js restores the persisted one).
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);

        await new HostPageSetup().EnsureAsync(dir.Path);
        Assert.DoesNotContain("dir=", dir.Read("wwwroot/index.html"));

        // An existing dir is the app's to own - never rewritten either.
        using var dir2 = new TempDir();
        dir2.Write("wwwroot/index.html", WasmIndex.Replace("<html lang=\"en\">", "<html lang=\"en\" dir=\"ltr\">"));
        await new HostPageSetup().EnsureAsync(dir2.Path);
        Assert.Contains("dir=\"ltr\"", dir2.Read("wwwroot/index.html"));
    }

    [Fact]
    public async Task A_fingerprinted_stylesheet_link_counts_as_present()
    {
        // Blazor's fingerprinted asset syntax nests quotes inside the href, so no attribute parse
        // (and no literal href match) sees it. The wiring must: injecting the plain link beside it
        // ships a 404 for a file that never exists (hit in a dogfood app, 2026-08-16).
        using var dir = new TempDir();
        dir.Write("Components/App.razor",
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <base href="/" />
                <link rel="stylesheet" href="@Assets["dist/app.css"]" />
                <HeadOutlet />
            </head>
            <body>
                <Routes />
            </body>
            </html>
            """);

        var result = await new HostPageSetup().EnsureAsync(dir.Path);

        var html = dir.Read("Components/App.razor");
        Assert.DoesNotContain("<link rel=\"stylesheet\" href=\"app.css\" />", html);
        Assert.Contains("boot.js", html); // the rest of the wiring still lands
        Assert.DoesNotContain(result.Changes, c => c.Contains("stylesheet"));
    }

    [Fact]
    public async Task A_bundler_pathed_or_cache_busted_link_counts_as_present()
    {
        foreach (var href in new[] { "dist/app.css", "app.css?v=3", "/app.css" })
        {
            using var dir = new TempDir();
            dir.Write("wwwroot/index.html", WasmIndex.Replace("<title>App</title>",
                $"<title>App</title>\n    <link rel=\"stylesheet\" href=\"{href}\" />"));

            await new HostPageSetup().EnsureAsync(dir.Path);

            var html = dir.Read("wwwroot/index.html");
            Assert.DoesNotContain("<link rel=\"stylesheet\" href=\"app.css\" />", html);
        }
    }

    [Fact]
    public async Task A_different_stylesheet_does_not_count()
    {
        // site.css may be anything - the CLI cannot know it carries the compiled output, and a
        // partial name match (myapp.css) is not the file either. The link is still inserted.
        foreach (var href in new[] { "site.css", "myapp.css" })
        {
            using var dir = new TempDir();
            dir.Write("wwwroot/index.html", WasmIndex.Replace("<title>App</title>",
                $"<title>App</title>\n    <link rel=\"stylesheet\" href=\"{href}\" />"));

            await new HostPageSetup().EnsureAsync(dir.Path);

            Assert.Contains("<link rel=\"stylesheet\" href=\"app.css\" />", dir.Read("wwwroot/index.html"));
        }
    }

    [Fact]
    public async Task Uninstall_leaves_an_app_authored_stylesheet_link_alone()
    {
        // Ensure never wrote a link here (the fingerprinted one counts as present), so uninstall
        // must not strip it either - RemoveAsync only ever matches the literal line Ensure writes.
        using var dir = new TempDir();
        dir.Write("Components/App.razor",
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <link rel="stylesheet" href="@Assets["dist/app.css"]" />
            </head>
            <body></body>
            </html>
            """);
        var setup = new HostPageSetup();
        await setup.EnsureAsync(dir.Path);

        await setup.RemoveAsync(dir.Path);

        var html = dir.Read("Components/App.razor");
        Assert.Contains("@Assets[\"dist/app.css\"]", html);
        Assert.DoesNotContain("boot.js", html); // its own wiring does come back out
    }

    [Fact]
    public async Task IsWired_tracks_the_boot_script()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);
        var setup = new HostPageSetup();

        Assert.False(setup.IsWired(dir.Path));
        await setup.EnsureAsync(dir.Path);
        Assert.True(setup.IsWired(dir.Path));
    }

    [Fact]
    public void IsWired_is_false_without_a_host()
    {
        using var dir = new TempDir();
        Assert.False(new HostPageSetup().IsWired(dir.Path));
    }

    [Fact]
    public async Task Wires_a_blazor_web_app_shell()
    {
        using var dir = new TempDir();
        dir.Write("Components/App.razor",
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <base href="/" />
                <HeadOutlet />
            </head>
            <body>
                <Routes />
                <script src="_framework/blazor.web.js"></script>
            </body>
            </html>
            """);

        var result = await new HostPageSetup().EnsureAsync(dir.Path);

        Assert.Equal("Components/App.razor", result.HostPath);
        Assert.Contains("boot.js", dir.Read("Components/App.razor"));
    }

    [Fact]
    public async Task A_router_app_razor_is_not_a_host()
    {
        using var dir = new TempDir();
        // WASM root App.razor is the Router - no <head>, must not be patched.
        dir.Write("App.razor", "<Router AppAssembly=\"@typeof(App).Assembly\"></Router>");

        var result = await new HostPageSetup().EnsureAsync(dir.Path);

        Assert.Null(result.HostPath);
        Assert.DoesNotContain("boot.js", dir.Read("App.razor"));
    }

    [Fact]
    public async Task No_host_reports_nothing_for_a_class_library()
    {
        using var dir = new TempDir();

        var result = await new HostPageSetup().EnsureAsync(dir.Path);

        Assert.Null(result.HostPath);
        Assert.Empty(result.Changes);
    }
}
