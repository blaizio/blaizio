using System.Text.Json;
using Blaizio.Cli.Commands;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// <c>blaizio build</c> end-to-end: plain registries build exactly as before; a manifest sitting
/// next to Styles/ (shared.css + style-*.css) additionally emits per-skin inlined item variants
/// under <c>{output}/{skin}/</c> and records the skin list in the index.
/// </summary>
public class BuildCommandTests
{
    private static CommandAppTester App()
    {
        var tester = new CommandAppTester();
        tester.Configure(CliApp.Configure);
        return tester;
    }

    private static void WriteManifest(TempDir dir)
    {
        dir.Write("src/registry.json",
            """
            {
              "name": "test",
              "items": [
                {
                  "name": "button",
                  "type": "registry:ui",
                  "files": [{ "path": "Button/BzButton.razor", "type": "registry:ui" }]
                }
              ]
            }
            """);
        dir.Write("src/Button/BzButton.razor",
            """
            <button class="bz-button extra">x</button>
            """);
    }

    [Fact]
    public async Task Min_base_rides_through_the_built_item_and_the_index()
    {
        using var dir = new TempDir();
        dir.Write("src/registry.json",
            """
            {
              "name": "test",
              "items": [
                {
                  "name": "panel",
                  "type": "registry:ui",
                  "minBase": "0.1.0-alpha.24",
                  "files": [{ "path": "Panel/BzPanel.razor", "type": "registry:ui" }]
                }
              ]
            }
            """);
        dir.Write("src/Panel/BzPanel.razor", "<aside>x</aside>");

        var result = await App().RunAsync("build", "./src/registry.json", "-o", "./r", "-s", "-c", dir.Path);

        Assert.Equal(0, result.ExitCode);
        // Both the served item and the catalogue entry carry it: add reads the item, gallery
        // tooling reads the index. A field dropped by the explicit copies would vanish here.
        using var item = JsonDocument.Parse(File.ReadAllText(dir.Combine("r", "panel.json")));
        Assert.Equal("0.1.0-alpha.24", item.RootElement.GetProperty("minBase").GetString());
        using var index = JsonDocument.Parse(File.ReadAllText(dir.Combine("r", "index.json")));
        var entry = index.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("0.1.0-alpha.24", entry.GetProperty("minBase").GetString());
    }

    [Fact]
    public async Task Plain_registry_builds_without_skin_variants()
    {
        using var dir = new TempDir();
        WriteManifest(dir);

        var result = await App().RunAsync("build", "./src/registry.json", "-o", "./r", "-s", "-c", dir.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(dir.Combine("r", "button.json")));
        using var index = JsonDocument.Parse(File.ReadAllText(dir.Combine("r", "index.json")));
        Assert.False(index.RootElement.TryGetProperty("styles", out _));
        // Content stays raw - tokens untouched.
        Assert.Contains("bz-button extra", File.ReadAllText(dir.Combine("r", "button.json")));
    }

    [Fact]
    public async Task Styles_next_to_the_manifest_emit_per_skin_inlined_variants()
    {
        using var dir = new TempDir();
        WriteManifest(dir);
        dir.Write("src/Styles/shared.css", ".bz-button { @apply border; }");
        dir.Write("src/Styles/style-test.css", ".style-test { .bz-button { @apply rounded-md; } }");
        dir.Write("src/Styles/style-other.css", ".style-other { .bz-button { @apply rounded-none; } }");

        var result = await App().RunAsync("build", "./src/registry.json", "-o", "./r", "-s", "-c", dir.Path);

        Assert.Equal(0, result.ExitCode);

        // Root output stays raw for v1 consumers.
        Assert.Contains("bz-button extra", File.ReadAllText(dir.Combine("r", "button.json")));

        // Per-skin variants carry the substituted utilities.
        var test = File.ReadAllText(dir.Combine("r", "test", "button.json"));
        Assert.Contains("border rounded-md extra", test);
        Assert.DoesNotContain("bz-button", test);
        var other = File.ReadAllText(dir.Combine("r", "other", "button.json"));
        Assert.Contains("border rounded-none extra", other);

        // The index records the skin list.
        using var index = JsonDocument.Parse(File.ReadAllText(dir.Combine("r", "index.json")));
        var styles = index.RootElement.GetProperty("styles").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(["other", "test"], styles);
    }

    [Fact]
    public async Task A_file_path_escaping_the_manifest_dir_fails_the_build()
    {
        using var dir = new TempDir();
        dir.Write("outside.razor", "<secret />");
        dir.Write("src/registry.json",
            """
            {
              "name": "test",
              "items": [
                {
                  "name": "button",
                  "type": "registry:ui",
                  "files": [{ "path": "../outside.razor", "type": "registry:ui" }]
                }
              ]
            }
            """);

        var result = await App().RunAsync("build", "./src/registry.json", "-o", "./r", "-s", "-c", dir.Path);

        Assert.Equal(1, result.ExitCode);
        // Nothing was written: the manifest is rejected before any output file is created.
        Assert.False(File.Exists(dir.Combine("r", "button.json")));
    }

    [Fact]
    public async Task An_item_name_with_a_path_separator_fails_the_build()
    {
        using var dir = new TempDir();
        dir.Write("src/Button/BzButton.razor", "<button>x</button>");
        dir.Write("src/registry.json",
            """
            {
              "name": "test",
              "items": [
                {
                  "name": "../evil",
                  "type": "registry:ui",
                  "files": [{ "path": "Button/BzButton.razor", "type": "registry:ui" }]
                }
              ]
            }
            """);

        var result = await App().RunAsync("build", "./src/registry.json", "-o", "./r", "-s", "-c", dir.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.False(File.Exists(dir.Combine("evil.json")));
    }
}
