using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The published schemas are hand-written JSON served from the docs site, and the wire model is
/// C#. Nothing compiles the two together, so these tests are the join: every serialized property
/// has to appear in the schema, and the schema cannot describe a property that no longer exists.
/// </summary>
public class RegistrySchemaTests
{
    [Fact]
    public void The_registry_schema_covers_every_manifest_property()
        => AssertCovers<RegistryIndex>("registry.json", root => root);

    [Fact]
    public void The_item_schema_covers_every_item_property()
        => AssertCovers<RegistryItem>("registry-item.json", root => root);

    [Fact]
    public void The_item_schema_covers_every_file_property()
        => AssertCovers<RegistryFile>("registry-item.json", root => root.GetProperty("$defs").GetProperty("file"));

    [Fact]
    public void The_item_schema_covers_every_font_property()
        => AssertCovers<FontSpec>("registry-item.json", root => root.GetProperty("$defs").GetProperty("font"));

    [Fact]
    public void The_schema_ids_match_the_urls_the_cli_writes()
    {
        Assert.Equal(RegistrySchema.Registry, Read("registry.json").RootElement.GetProperty("$id").GetString());
        Assert.Equal(RegistrySchema.Item, Read("registry-item.json").RootElement.GetProperty("$id").GetString());
    }

    [Fact]
    public void The_item_schema_lists_every_item_type()
    {
        var declared = Read("registry-item.json").RootElement
            .GetProperty("properties").GetProperty("type").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);

        foreach (var value in Enum.GetValues<ItemType>())
            Assert.Contains(WireName(value), declared);
    }

    private static void AssertCovers<T>(string schemaFile, Func<JsonElement, JsonElement> section)
    {
        using var schema = Read(schemaFile);
        var described = section(schema.RootElement).GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // Every property the CLI serializes must be describable by an editor...
        // [JsonIgnore] alone drops a property; [JsonIgnore(Condition = WhenWritingNull)] only omits
        // it when unset, so it is still part of the wire shape and still needs describing.
        var serialized = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>()
                is null or not { Condition: JsonIgnoreCondition.Always })
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name)
            .ToList();
        foreach (var name in serialized)
            Assert.Contains(name, described);

        // ...and the schema must not describe fields that no longer exist ($schema is ours).
        foreach (var name in described.Where(n => n != "$schema"))
            Assert.Contains(name, serialized);
    }

    private static string WireName(ItemType type) =>
        typeof(ItemType).GetField(type.ToString())!
            .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;

    private static JsonDocument Read(string file) =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "docs", "Blaizio.Docs", "wwwroot", "schema", file)));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blaizio.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Blaizio.slnx not found above the test binary.");
    }
}
