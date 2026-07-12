using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class RegistryClientTests
{
    private static RegistryClient LocalClient(string baseDir) => new(new HttpClient(), baseDir);

    [Fact]
    public async Task Reads_index_from_a_local_registry()
    {
        using var dir = new TempDir();
        dir.Write("index.json", """{ "name": "test", "items": [ { "name": "button", "type": "registry:ui" } ] }""");

        var index = await LocalClient(dir.Path).GetIndexAsync();

        Assert.Equal("test", index.Name);
        Assert.Single(index.Items);
        Assert.Equal("button", index.Items[0].Name);
    }

    [Fact]
    public async Task Reads_an_item_by_name_and_parses_the_type()
    {
        using var dir = new TempDir();
        dir.Write("button.json", """
            { "name": "button", "type": "registry:ui", "registryDependencies": ["utils"],
              "files": [ { "path": "Ui/Button/Button.razor", "type": "registry:ui", "content": "x" } ] }
            """);

        var item = await LocalClient(dir.Path).GetItemAsync("button");

        Assert.Equal("button", item.Name);
        Assert.Equal(ItemType.Ui, item.Type);
        Assert.Equal(["utils"], item.RegistryDependencies);
        Assert.Equal("x", item.Files[0].Content);
    }

    [Fact]
    public async Task Omitted_collections_deserialize_as_empty_not_null()
    {
        using var dir = new TempDir();
        dir.Write("utils.json", """{ "name": "utils", "type": "registry:lib" }""");

        var item = await LocalClient(dir.Path).GetItemAsync("utils");

        Assert.Empty(item.RegistryDependencies);
        Assert.Empty(item.NugetDependencies);
        Assert.Empty(item.Files);
    }

    [Theory]
    [InlineData("InputText")]  // PascalCase from the command line
    [InlineData("input-text")] // already kebab (a registryDependency reference)
    public async Task Resolves_a_pascal_case_name_to_the_kebab_case_file(string reference)
    {
        using var dir = new TempDir();
        dir.Write("input-text.json", """{ "name": "input-text", "type": "registry:ui" }""");

        var item = await LocalClient(dir.Path).GetItemAsync(reference);

        Assert.Equal("input-text", item.Name);
    }

    [Fact]
    public async Task Throws_a_registry_exception_for_a_missing_file()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<RegistryException>(() => LocalClient(dir.Path).GetItemAsync("nope"));
    }
}
