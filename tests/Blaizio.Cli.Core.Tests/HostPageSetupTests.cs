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

        var result = await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: false);

        Assert.Equal("wwwroot/index.html", result.HostPath);
        var html = dir.Read("wwwroot/index.html");
        Assert.Contains("class=\"style-ember\"", html);
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

        await setup.EnsureAsync(dir.Path, "ember", rtl: false);
        var afterFirst = dir.Read("wwwroot/index.html");
        var second = await setup.EnsureAsync(dir.Path, "ember", rtl: false);

        Assert.Empty(second.Changes);
        Assert.Equal(afterFirst, dir.Read("wwwroot/index.html"));
    }

    [Fact]
    public async Task Swaps_a_differing_skin_class_and_keeps_the_rest()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex.Replace("<html lang=\"en\">", "<html lang=\"en\" class=\"dark style-aura\">"));

        await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: false);

        var html = dir.Read("wwwroot/index.html");
        Assert.Contains("class=\"dark style-ember\"", html);
        Assert.DoesNotContain("style-aura", html);
    }

    [Fact]
    public async Task Adds_dir_rtl_only_when_asked_and_unset()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);

        await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: true);
        Assert.Contains("dir=\"rtl\"", dir.Read("wwwroot/index.html"));

        // An existing dir is the app's to own - never overwritten.
        using var dir2 = new TempDir();
        dir2.Write("wwwroot/index.html", WasmIndex.Replace("<html lang=\"en\">", "<html lang=\"en\" dir=\"ltr\">"));
        await new HostPageSetup().EnsureAsync(dir2.Path, "ember", rtl: true);
        Assert.DoesNotContain("dir=\"rtl\"", dir2.Read("wwwroot/index.html"));
    }

    [Fact]
    public async Task Adds_and_swaps_the_preset_class()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);
        var setup = new HostPageSetup();

        await setup.EnsureAsync(dir.Path, "ember", rtl: false, preset: "comet");
        Assert.Contains("class=\"style-ember preset-comet\"", dir.Read("wwwroot/index.html"));

        // A differing preset is swapped, everything else kept.
        await setup.EnsureAsync(dir.Path, "ember", rtl: false, preset: "nebula");
        var html = dir.Read("wwwroot/index.html");
        Assert.Contains("preset-nebula", html);
        Assert.DoesNotContain("preset-comet", html);
    }

    [Fact]
    public async Task Nova_removes_an_existing_preset_class()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex.Replace("<html lang=\"en\">", "<html lang=\"en\" class=\"style-ember preset-comet\">"));

        await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: false, preset: "nova");

        var html = dir.Read("wwwroot/index.html");
        Assert.DoesNotContain("preset-", html);
        Assert.Contains("style-ember", html);
    }

    [Fact]
    public async Task Preset_run_is_idempotent()
    {
        using var dir = new TempDir();
        dir.Write("wwwroot/index.html", WasmIndex);
        var setup = new HostPageSetup();

        await setup.EnsureAsync(dir.Path, "ember", rtl: false, preset: "comet");
        var afterFirst = dir.Read("wwwroot/index.html");
        var second = await setup.EnsureAsync(dir.Path, "ember", rtl: false, preset: "comet");

        Assert.Empty(second.Changes);
        Assert.Equal(afterFirst, dir.Read("wwwroot/index.html"));
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

        var result = await new HostPageSetup().EnsureAsync(dir.Path, "flint", rtl: false);

        Assert.Equal("Components/App.razor", result.HostPath);
        var razor = dir.Read("Components/App.razor");
        Assert.Contains("style-flint", razor);
        Assert.Contains("boot.js", razor);
    }

    [Fact]
    public async Task A_router_app_razor_is_not_a_host()
    {
        using var dir = new TempDir();
        // WASM root App.razor is the Router - no <head>, must not be patched.
        dir.Write("App.razor", "<Router AppAssembly=\"@typeof(App).Assembly\"></Router>");

        var result = await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: false);

        Assert.Null(result.HostPath);
        Assert.DoesNotContain("boot.js", dir.Read("App.razor"));
    }

    [Fact]
    public async Task No_host_reports_nothing_for_a_class_library()
    {
        using var dir = new TempDir();

        var result = await new HostPageSetup().EnsureAsync(dir.Path, "ember", rtl: false);

        Assert.Null(result.HostPath);
        Assert.Empty(result.Changes);
    }
}
