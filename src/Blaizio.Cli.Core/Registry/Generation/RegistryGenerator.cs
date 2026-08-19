using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Styling;

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

    /// <summary>
    /// Extra NuGet packages a specific family needs on top of <see cref="UiNuget"/>, keyed by the
    /// kebab-case item name. The scan can't infer these (a <c>using</c> doesn't name a package id),
    /// so families with third-party encoders register here.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FamilyNuget { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["qr-code"] = ["Net.Codecrete.QrCodeGenerator"],
        };

    /// <summary>
    /// The lowest <c>Blaizio.Base</c> a family works against, keyed by kebab-case item name -
    /// set when a family calls into a Base capability (typically a JS module) that shipped in a
    /// specific release, and never bumped after: it records where the capability appeared, not
    /// what is current. Families absent here carry no <c>minBase</c> and skip the add-time check.
    /// </summary>
    public IReadOnlyDictionary<string, string> FamilyMinBase { get; init; } =
        new Dictionary<string, string>
        {
            // panel.ts (the drag-resize module) shipped in alpha.24; alpha.25 added the drag-cursor
            // argument the component now passes, which an older module would silently ignore.
            ["panel"] = "0.1.0-alpha.25",
        };

    /// <summary>
    /// Root-level source files to leave OUT of the shared <c>utils</c> item. Defaults to the
    /// app-wide DI registration, which references component services (Dialog/Toast) and so is
    /// optional glue, not a leaf helper every component can depend on.
    /// </summary>
    public IReadOnlyList<string> ExcludedSharedFiles { get; init; } = ["ServiceCollectionExtensions.cs"];
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

    // Comment forms whose Bz* mentions must NOT become dependencies: a doc line saying "like
    // BzInputText" would otherwise ship input-text with every input-number install. Razor
    // (@* *@), C# block (/* */, including ///-style XML docs via the line rule), HTML (<!-- -->),
    // and line comments — the (?<!:) guard keeps "https://" inside code from eating the line.
    [GeneratedRegex(@"@\*.*?\*@|/\*.*?\*/|<!--.*?-->|(?<!:)//[^\r\n]*", RegexOptions.Singleline)]
    private static partial Regex CommentToken();

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
                // Dependencies come from CODE ONLY: comments routinely name other components
                // ("mirrors BzInputText's setter") and must not pull them into every install.
                foreach (Match m in BzToken().Matches(CommentToken().Replace(File.ReadAllText(file), " ")))
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
                NugetDependencies = _options.FamilyNuget.TryGetValue(name, out var extra)
                    ? [.. _options.UiNuget, .. extra]
                    : _options.UiNuget,
                MinBase = _options.FamilyMinBase.GetValueOrDefault(name),
                RegistryDependencies = [.. deps],
                Files = [.. files.Select(f => new RegistryFile
                {
                    Path = RelPath(sourceRoot, f),
                    Type = FileType.Ui,
                })],
            });
        }

        // One body + one heading item per offered webfont (font-inter / font-heading-inter),
        // generated straight from FontCatalog so the registry can't drift from the CLI's list.
        foreach (var font in FontCatalog.All.Where(f => f is { IsWebFont: true, Offered: true }))
        {
            items.Add(new RegistryItem
            {
                Name = $"font-{font.Name}",
                Type = ItemType.Font,
                Title = font.Title,
                Description = $"{font.Title} as the body font.",
                Font = new FontSpec { Name = font.Name },
            });
            items.Add(new RegistryItem
            {
                Name = $"font-heading-{font.Name}",
                Type = ItemType.Font,
                Title = $"{font.Title} (Heading)",
                Description = $"{font.Title} as the heading face (--font-heading).",
                Font = new FontSpec { Name = font.Name, Heading = true },
            });
        }

        // The typeset prose stylesheet - a universal file item that lands next to the consumer's
        // app.css. Emitted only when the source tree ships it, so other trees fed through the
        // generator don't grow a phantom item.
        var typesetCss = Path.Combine(sourceRoot, "Styles", "typeset.css");
        if (File.Exists(typesetCss))
        {
            items.Add(new RegistryItem
            {
                Name = "typeset",
                Type = ItemType.File,
                Title = "Typeset",
                Description = "A prose stylesheet for HTML without classes: rendered markdown, CMS output, streaming chat. Import it after Tailwind, wrap content in .typeset, tune three knobs.",
                Files = [new RegistryFile
                {
                    Path = "Styles/typeset.css",
                    Type = FileType.File,
                    Target = "~/Styles/typeset.css",
                }],
            });
        }

        // The $schema key is the whole reason an editor can complete this file, so a generated
        // manifest carries it from the first line rather than waiting to be added by hand.
        return new RegistryIndex { Schema = RegistrySchema.Registry, Name = _options.Name, Items = items };
    }

    /// <summary>The shared-lib item: root-level helpers plus the Extensions folder.</summary>
    private RegistryItem? BuildUtilsItem(string sourceRoot)
    {
        var excluded = new HashSet<string>(_options.ExcludedSharedFiles, StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !excluded.Contains(Path.GetFileName(f)))
            .ToList();
        var extensions = Path.Combine(sourceRoot, "Extensions");
        if (Directory.Exists(extensions))
            files.AddRange(Directory.GetFiles(extensions, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excluded.Contains(Path.GetFileName(f))));

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
                .Select(f => new RegistryFile { Path = RelPath(sourceRoot, f), Type = FileType.Lib })],
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
