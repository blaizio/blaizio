using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>build</c> (maintainer command).</summary>
public sealed class BuildSettings : GlobalSettings
{
    /// <summary>Path to the source <c>registry.json</c> manifest.</summary>
    // [manifest], not [registry]: the positional is a local file path, --registry is a URL.
    [CommandArgument(0, "[manifest]")]
    [Description("Path to the source registry.json (default: ./registry.json).")]
    [DefaultValue("./registry.json")]
    public string Manifest { get; init; } = "./registry.json";

    /// <summary>Directory the resolved item JSON files are written to.</summary>
    [CommandOption("-o|--output <dir>")]
    [Description("Output directory (default: ./public/r).")]
    [DefaultValue("./public/r")]
    public string Output { get; init; } = "./public/r";
}

/// <summary>
/// Compiles a source <c>registry.json</c> (items with on-disk file paths) into per-item resolved
/// JSON with inline file contents, plus an <c>index.json</c> catalogue. When the manifest sits
/// next to a <c>Styles/</c> dir (shared.css + style-*.css), it additionally emits one INLINED
/// variant of every item per skin under <c>{output}/{skin}/</c> - the shipped source with each
/// <c>bz-*</c> token substituted by that skin's utilities (<see cref="SkinInliner"/>) - and the
/// index records the skin list in <c>styles</c>. Run by the library authors.
/// </summary>
public sealed class BuildCommand : AsyncCommand<BuildSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings)
    {
        var manifestPath = Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Manifest));
        if (!File.Exists(manifestPath))
        {
            settings.Warn($"[red]Manifest not found:[/] {Markup.Escape(manifestPath)}");
            return 1;
        }

        var manifestDir = Path.GetDirectoryName(manifestPath)!;
        var outputDir = Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Output));

        // Reads the manifest and folds in whatever it includes: from here down there is one flat
        // item list, with every path expressed against this manifest's directory.
        var loaded = await ManifestLoader.LoadAsync(manifestPath);
        var manifest = loaded.Manifest;

        // Validate names and source files up front so a missing one can't leave a half-written
        // output dir - and so a crafted manifest can't read outside its own directory or write
        // outside the output dir through an item name.
        var problems = new List<string>(loaded.Problems.Select(p => $"[red]Include:[/] {Markup.Escape(p)}"));
        foreach (var item in manifest.Items)
        {
            if (!RegistryValidateCommand.IsSafeItemName(item.Name))
            {
                problems.Add($"[red]Invalid item name[/] '{Markup.Escape(item.Name ?? "(none)")}': names are slugs (letters, digits, '-', '_', '.').");
                continue;
            }
            foreach (var file in item.Files)
            {
                string source;
                try
                {
                    source = SafePath.Resolve(manifestDir, file.Path);
                }
                catch (InvalidOperationException)
                {
                    problems.Add($"[red]Escaping path for '{Markup.Escape(item.Name)}':[/] {Markup.Escape(file.Path)} resolves outside the manifest directory.");
                    continue;
                }
                if (!File.Exists(source))
                    problems.Add($"[red]Missing file for '{Markup.Escape(item.Name)}':[/] {Markup.Escape(source)}");
            }
        }
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
                settings.Warn(problem);
            return 1;
        }

        var inliners = LoadInliners(manifestDir);
        Directory.CreateDirectory(outputDir);
        foreach (var skin in inliners.Keys)
            Directory.CreateDirectory(Path.Combine(outputDir, skin));

        var indexItems = new List<RegistryItem>(manifest.Items.Count);

        foreach (var item in manifest.Items)
        {
            var resolvedFiles = new List<RegistryFile>(item.Files.Count);
            foreach (var file in item.Files)
            {
                var source = SafePath.Resolve(manifestDir, file.Path);

                resolvedFiles.Add(new RegistryFile
                {
                    Path = file.Path,
                    Type = file.Type,
                    Target = file.Target,
                    Content = await File.ReadAllTextAsync(source),
                });
            }

            await WriteItemAsync(Path.Combine(outputDir, $"{item.Name}.json"), item, resolvedFiles);

            // Per-skin variants: the same item with every mapped bz-* token in the file contents
            // substituted by the skin's utilities.
            foreach (var (skin, inliner) in inliners)
            {
                var inlined = resolvedFiles
                    .Select(f => new RegistryFile
                    {
                        Path = f.Path,
                        Type = f.Type,
                        Target = f.Target,
                        Content = inliner.Inline(f.Content!),
                    })
                    .ToList();
                await WriteItemAsync(Path.Combine(outputDir, skin, $"{item.Name}.json"), item, inlined);
            }

            // The catalogue entry omits file contents (name/metadata only).
            indexItems.Add(new RegistryItem
            {
                Name = item.Name,
                Type = item.Type,
                Title = item.Title,
                Version = item.Version,
                Description = item.Description,
                Author = item.Author,
                Categories = item.Categories,
                Docs = item.Docs,
                Meta = item.Meta,
                NugetDependencies = item.NugetDependencies,
                DevDependencies = item.DevDependencies,
                RegistryDependencies = item.RegistryDependencies,
            });
        }

        var index = new RegistryIndex
        {
            Schema = RegistrySchema.Registry,
            Name = manifest.Name,
            Items = indexItems,
            Styles = inliners.Count > 0 ? [.. inliners.Keys] : null,
        };
        await using (var indexStream = File.Create(Path.Combine(outputDir, "index.json")))
            await JsonSerializer.SerializeAsync(indexStream, index, CoreJson.Default.RegistryIndex);

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(index, CliJson.Default.RegistryIndex));
            return 0;
        }

        var skins = inliners.Count > 0 ? $" ({inliners.Count} skin variants)" : "";
        settings.Line($"[green]Built[/] {indexItems.Count} item(s){skins} to {Markup.Escape(outputDir)}.");
        return 0;
    }

    /// <summary>
    /// One inliner per skin sheet found next to the manifest (<c>Styles/style-*.css</c> with
    /// <c>Styles/shared.css</c>); empty when the registry has no style sheets - plain registries
    /// build exactly as before.
    /// </summary>
    private static SortedDictionary<string, SkinInliner> LoadInliners(string manifestDir)
    {
        var inliners = new SortedDictionary<string, SkinInliner>(StringComparer.Ordinal);
        var stylesDir = Path.Combine(manifestDir, "Styles");
        var sharedPath = Path.Combine(stylesDir, "shared.css");
        if (!File.Exists(sharedPath))
            return inliners;

        var shared = File.ReadAllText(sharedPath);
        foreach (var skinPath in Directory.EnumerateFiles(stylesDir, "style-*.css"))
        {
            var skin = Path.GetFileNameWithoutExtension(skinPath)["style-".Length..];
            inliners[skin] = SkinInliner.Create(shared, File.ReadAllText(skinPath));
        }
        return inliners;
    }

    private static async Task WriteItemAsync(string path, RegistryItem item, IReadOnlyList<RegistryFile> files)
    {
        var resolved = new RegistryItem
        {
            Schema = RegistrySchema.Item,
            Name = item.Name,
            Type = item.Type,
            Title = item.Title,
            Version = item.Version,
            Description = item.Description,
            Author = item.Author,
            Categories = item.Categories,
            Docs = item.Docs,
            Meta = item.Meta,
            DevDependencies = item.DevDependencies,
            NugetDependencies = item.NugetDependencies,
            RegistryDependencies = item.RegistryDependencies,
            Files = files,
            CssVars = item.CssVars,
            Css = item.Css,
            Font = item.Font,
        };
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, resolved, CoreJson.Default.RegistryItem);
    }
}
