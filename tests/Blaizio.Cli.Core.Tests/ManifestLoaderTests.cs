using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// `include` lets a registry keep one manifest per folder. What matters is that the flattened
/// result is indistinguishable from having written one big file: same items, same paths.
/// </summary>
public class ManifestLoaderTests
{
    [Fact]
    public async Task Folds_an_included_manifest_and_rebases_its_paths()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """
            {
              "name": "acme",
              "include": ["Components/registry.json"],
              "items": [
                { "name": "root-item", "type": "registry:lib",
                  "files": [{ "path": "Lib/Root.cs", "type": "registry:lib" }] }
              ]
            }
            """);
        dir.Write("Components/registry.json", """
            {
              "items": [
                { "name": "tag", "type": "registry:ui",
                  "files": [{ "path": "Tag/BzTag.razor", "type": "registry:ui" }] }
              ]
            }
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        Assert.Empty(loaded.Problems);
        Assert.Equal("acme", loaded.Manifest.Name);
        Assert.Equal(["root-item", "tag"], loaded.Manifest.Items.Select(i => i.Name));
        // The included file's own path was relative to ITS manifest; it now reads from the root.
        Assert.Equal("Components/Tag/BzTag.razor", loaded.Manifest.Items[1].Files[0].Path);
        Assert.Equal("Lib/Root.cs", loaded.Manifest.Items[0].Files[0].Path);
    }

    [Fact]
    public async Task Includes_are_transitive()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """{"name":"acme","include":["a/registry.json"],"items":[]}""");
        dir.Write("a/registry.json", """{"include":["b/registry.json"],"items":[]}""");
        dir.Write("a/b/registry.json", """
            {"items":[{"name":"deep","type":"registry:ui","files":[{"path":"Deep.razor","type":"registry:ui"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        Assert.Empty(loaded.Problems);
        Assert.Equal("a/b/Deep.razor", loaded.Manifest.Items.Single().Files[0].Path);
    }

    [Fact]
    public async Task A_cycle_terminates_instead_of_recursing()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """{"name":"acme","include":["a/registry.json"],"items":[]}""");
        dir.Write("a/registry.json", """
            {"include":["../registry.json"],"items":[{"name":"one","type":"registry:ui","files":[{"path":"One.razor"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        Assert.Equal(["one"], loaded.Manifest.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task A_duplicate_name_across_files_names_both_files()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """
            {"name":"acme","include":["a/registry.json"],
             "items":[{"name":"tag","type":"registry:ui","files":[{"path":"Tag.razor"}]}]}
            """);
        dir.Write("a/registry.json", """
            {"items":[{"name":"tag","type":"registry:ui","files":[{"path":"Tag.razor"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        var problem = Assert.Single(loaded.Problems);
        Assert.Contains("duplicate item name 'tag'", problem);
        Assert.Contains("registry.json", problem);
        Assert.Contains("a/registry.json", problem);
        // The first declaration wins, so a duplicate never silently replaces a real item.
        Assert.Equal("Tag.razor", loaded.Manifest.Items.Single().Files[0].Path);
    }

    [Theory]
    [InlineData("[\"missing/registry.json\"]", "does not exist")]
    [InlineData("[\"Components\"]", "includes a folder")]
    [InlineData("[\"../outside/registry.json\"]", "outside the registry")]
    public async Task Reports_a_bad_include(string include, string expected)
    {
        using var dir = new TempDir();
        dir.Write("registry.json", $$"""{"name":"acme","include":{{include}},"items":[]}""");
        Directory.CreateDirectory(dir.Combine("Components"));

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        Assert.Contains(loaded.Problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_root_manifests_escaping_path_is_left_for_the_existing_checks()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """
            {"name":"acme","items":[{"name":"escape","type":"registry:ui","files":[{"path":"../outside.razor"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        // Untouched: validate and build both resolve this path themselves and say so in their
        // own words, which is what their tests and their users already expect.
        Assert.Empty(loaded.Problems);
        Assert.Equal("../outside.razor", loaded.Manifest.Items.Single().Files[0].Path);
    }

    [Fact]
    public async Task A_file_reaching_outside_the_registry_is_reported_not_rebased()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """{"name":"acme","include":["a/registry.json"],"items":[]}""");
        dir.Write("a/registry.json", """
            {"items":[{"name":"escape","type":"registry:ui","files":[{"path":"../../elsewhere/X.razor"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        // Reported with the manifest it was written in, and the file is dropped rather than
        // rebased into something that would resolve somewhere else entirely.
        Assert.Contains(loaded.Problems, p => p.Contains("escapes the registry", StringComparison.Ordinal));
        Assert.Contains("a/registry.json", loaded.Problems[0]);
        Assert.Empty(loaded.Manifest.Items.Single().Files);
    }

    [Fact]
    public async Task A_manifest_without_includes_loads_unchanged()
    {
        using var dir = new TempDir();
        dir.Write("registry.json", """
            {"name":"acme","items":[{"name":"tag","type":"registry:ui","files":[{"path":"Tag.razor"}]}]}
            """);

        var loaded = await ManifestLoader.LoadAsync(dir.Combine("registry.json"));

        Assert.Empty(loaded.Problems);
        Assert.Equal("Tag.razor", loaded.Manifest.Items.Single().Files[0].Path);
    }
}
