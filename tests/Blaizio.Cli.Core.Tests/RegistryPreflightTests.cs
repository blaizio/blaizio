using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class RegistryPreflightTests
{
    private static RegistryClient LocalClient(string baseDir, string? style = null)
        => new(new HttpClient(), baseDir, style);

    [Fact]
    public async Task A_registry_with_an_index_passes()
    {
        using var dir = new TempDir();
        dir.Write("index.json", """{ "name": "test", "items": [] }""");

        var status = await RegistryPreflight.CheckAsync(LocalClient(dir.Path));

        Assert.True(status.Reachable);
        Assert.True(status.HasIndex);
        Assert.Null(status.Message);
    }

    [Fact]
    public async Task A_reachable_registry_without_an_index_passes_but_reports_no_index()
    {
        // v1 (raw sources) and third-party registries ship items with no index.json - the run must
        // proceed, because items still resolve at the base path.
        using var dir = new TempDir();
        dir.Write("button.json", """{ "name": "button", "type": "registry:ui" }""");

        var status = await RegistryPreflight.CheckAsync(LocalClient(dir.Path));

        Assert.True(status.Reachable);
        Assert.False(status.HasIndex);
    }

    [Fact]
    public async Task A_local_registry_path_that_does_not_exist_fails()
    {
        using var dir = new TempDir();

        var status = await RegistryPreflight.CheckAsync(LocalClient(dir.Combine("nope")));

        Assert.False(status.Reachable);
        Assert.Contains("not found", status.Message);
    }

    [Fact]
    public async Task Classifies_a_missing_file_as_NotFound_and_a_missing_directory_as_Unreachable()
    {
        using var dir = new TempDir();

        var missingFile = await Assert.ThrowsAsync<RegistryException>(
            () => LocalClient(dir.Path).GetIndexAsync());
        Assert.Equal(RegistryFailure.NotFound, missingFile.Reason);

        var missingDir = await Assert.ThrowsAsync<RegistryException>(
            () => LocalClient(dir.Combine("nope")).GetIndexAsync());
        Assert.Equal(RegistryFailure.Unreachable, missingDir.Reason);
    }

    [Fact]
    public async Task Classifies_malformed_json()
    {
        using var dir = new TempDir();
        dir.Write("index.json", "{ not json");

        var ex = await Assert.ThrowsAsync<RegistryException>(() => LocalClient(dir.Path).GetIndexAsync());

        Assert.Equal(RegistryFailure.Malformed, ex.Reason);
    }

    [Fact]
    public async Task An_item_resolves_from_an_indexless_registry_even_with_a_style_recorded()
    {
        // The skin-variant gate reads the index; a registry that has none ships no variants, so
        // the lookup must fall back to the base path instead of failing.
        using var dir = new TempDir();
        dir.Write("button.json", """{ "name": "button", "type": "registry:ui" }""");

        var item = await LocalClient(dir.Path, style: "ember").GetItemAsync("button");

        Assert.Equal("button", item.Name);
    }
}
