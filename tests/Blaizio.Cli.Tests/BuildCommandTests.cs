using System.Text.Json;
using Blaizio.Cli.Commands;
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
}
