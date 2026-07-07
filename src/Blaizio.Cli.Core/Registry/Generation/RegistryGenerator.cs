using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Registry.Generation;

/// <summary>Knobs for <see cref="RegistryGenerator"/>, defaulted to the Blaizio.Ui layout.</summary>
public sealed class GeneratorOptions
{
    /// <summary>Registry display name.</summary>
    public string Name { get; init; } = "blaizio";

    /// <summary>Folder under the source root that holds one sub-folder per component family.</summary>
    public string ComponentsDir { get; init; } = "Components";

    /// <summary>The shared-lib item name every component depends on.</summary>
    public string UtilsItem { get; init; } = "utils";

    /// <summary>NuGet packages every UI component needs.</summary>
    public IReadOnlyList<string> UiNuget { get; init; } = ["Blaizio.Base", "Blaizio.Icons", "TailwindMerge.NET"];

    /// <summary>NuGet packages the shared-lib item needs.</summary>
    public IReadOnlyList<string> UtilsNuget { get; init; } = ["TailwindMerge.NET"];
}

/// <summary>
/// Builds a registry manifest by scanning the Blaizio.Ui source tree: one item per component family
/// folder plus a shared <c>utils</c> lib item (the root helpers). Cross-component dependencies are
/// inferred by finding which other families' <c>Bz*</c> component types each family references, so
/// adding <c>alert-dialog</c> also pulls <c>button</c> and <c>dialog</c>. Emits paths only (no file
/// content) — feed the result through <c>build</c> to inline it.
/// </summary>
public sealed partial class RegistryGenerator(GeneratorOptions? options = null)
{
    private readonly GeneratorOptions _options = options ?? new GeneratorOptions();

    [GeneratedRegex(@"\bBz[A-Z][A-Za-z0-9]*\b")]
    private static partial Regex BzToken();

    /// <summary>Scan <paramref name="sourceRoot"/> (e.g. <c>src/Blaizio.Ui</c>) into a manifest.</summary>
    public RegistryIndex Generate(string sourceRoot)
    {
        var items = new List<RegistryItem>();

        var utils = BuildUtilsItem(sourceRoot);
        if (utils is not null)
            items.Add(utils);

        var componentsAbs = Path.Combine(sourceRoot, _options.ComponentsDir);
        if (!Directory.Exists(componentsAbs))
            throw new DirectoryNotFoundException($"Components directory not found: {componentsAbs}");

        var families = Directory.GetDirectories(componentsAbs)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Map every family's exported Bz* type -> the family's item name, for cross-ref inference.
        var typeToItem = new Dictionary<string, string>(StringComparer.Ordinal);
        var familyFiles = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var dir in families)
        {
            var name = ToKebab(new DirectoryInfo(dir).Name);
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(IsSource)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            familyFiles[name] = files;

            foreach (var file in files)
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.StartsWith("Bz", StringComparison.Ordinal))
                    typeToItem[stem] = name;
            }
        }

        foreach (var dir in families)
        {
            var name = ToKebab(new DirectoryInfo(dir).Name);
            var files = familyFiles[name];

            var deps = new SortedSet<string>(StringComparer.Ordinal) { _options.UtilsItem };
            foreach (var file in files)
            {
                foreach (Match m in BzToken().Matches(File.ReadAllText(file)))
                {
                    if (typeToItem.TryGetValue(m.Value, out var owner) && owner != name)
                        deps.Add(owner);
                }
            }

            items.Add(new RegistryItem
            {
                Name = name,
                Type = ItemType.Ui,
                Title = Humanize(new DirectoryInfo(dir).Name),
                NugetDependencies = _options.UiNuget,
                RegistryDependencies = [.. deps],
                Files = [.. files.Select(f => new RegistryFile
                {
                    Path = RelPath(sourceRoot, f),
                    Type = ItemType.Ui,
                })],
            });
        }

        return new RegistryIndex { Name = _options.Name, Items = items };
    }

    /// <summary>The shared-lib item: root-level helpers plus the Extensions folder.</summary>
    private RegistryItem? BuildUtilsItem(string sourceRoot)
    {
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly).ToList();
        var extensions = Path.Combine(sourceRoot, "Extensions");
        if (Directory.Exists(extensions))
            files.AddRange(Directory.GetFiles(extensions, "*.cs", SearchOption.AllDirectories));

        if (files.Count == 0)
            return null;

        return new RegistryItem
        {
            Name = _options.UtilsItem,
            Type = ItemType.Lib,
            Title = "Utilities",
            NugetDependencies = _options.UtilsNuget,
            Files = [.. files
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new RegistryFile { Path = RelPath(sourceRoot, f), Type = ItemType.Lib })],
        };
    }

    private static bool IsSource(string path)
        => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static string RelPath(string root, string file)
        => Path.GetRelativePath(root, file).Replace('\\', '/');

    /// <summary>PascalCase folder name to kebab-case item name (AlertDialog -&gt; alert-dialog).</summary>
    public static string ToKebab(string pascal)
        => string.Concat(pascal.Select((c, i) =>
            char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    /// <summary>PascalCase folder name to spaced words (AlertDialog -&gt; Alert Dialog).</summary>
    private static string Humanize(string pascal)
        => string.Concat(pascal.Select((c, i) => char.IsUpper(c) && i > 0 ? " " + c : c.ToString()));
}
