using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>build</c> (maintainer command).</summary>
public sealed class BuildSettings : GlobalSettings
{
    /// <summary>Path to the source <c>registry.json</c> manifest.</summary>
    [CommandArgument(0, "[MANIFEST]")]
    [Description("Path to the source registry.json (default: ./registry.json).")]
    [DefaultValue("./registry.json")]
    public string Manifest { get; init; } = "./registry.json";

    /// <summary>Directory the resolved item JSON files are written to.</summary>
    [CommandOption("-o|--output <DIR>")]
    [Description("Output directory (default: ./public/r).")]
    [DefaultValue("./public/r")]
    public string Output { get; init; } = "./public/r";
}

/// <summary>
/// Compiles a source <c>registry.json</c> (items with on-disk file paths) into per-item resolved
/// JSON with inline file contents, plus an <c>index.json</c> catalogue. Run by the library authors.
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

        RegistryIndex manifest;
        await using (var stream = File.OpenRead(manifestPath))
            manifest = await JsonSerializer.DeserializeAsync(stream, CoreJson.Default.RegistryIndex)
                ?? throw new InvalidDataException("registry.json is empty or malformed.");

        // Validate every source file up front so a missing one can't leave a half-written output dir.
        var missing = manifest.Items
            .SelectMany(item => item.Files.Select(file =>
                (item.Name, Source: Path.GetFullPath(Path.Combine(manifestDir, file.Path)))))
            .Where(f => !File.Exists(f.Source))
            .ToArray();
        if (missing.Length > 0)
        {
            foreach (var (name, sourcePath) in missing)
                settings.Warn($"[red]Missing file for '{Markup.Escape(name)}':[/] {Markup.Escape(sourcePath)}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);
        var indexItems = new List<RegistryItem>(manifest.Items.Count);

        foreach (var item in manifest.Items)
        {
            var resolvedFiles = new List<RegistryFile>(item.Files.Count);
            foreach (var file in item.Files)
            {
                var source = Path.GetFullPath(Path.Combine(manifestDir, file.Path));

                resolvedFiles.Add(new RegistryFile
                {
                    Path = file.Path,
                    Type = file.Type,
                    Target = file.Target,
                    Content = await File.ReadAllTextAsync(source),
                });
            }

            var resolved = new RegistryItem
            {
                Name = item.Name,
                Type = item.Type,
                Title = item.Title,
                Description = item.Description,
                NugetDependencies = item.NugetDependencies,
                RegistryDependencies = item.RegistryDependencies,
                Files = resolvedFiles,
                CssVars = item.CssVars,
                Tailwind = item.Tailwind,
            };

            await using var outStream = File.Create(Path.Combine(outputDir, $"{item.Name}.json"));
            await JsonSerializer.SerializeAsync(outStream, resolved, CoreJson.Default.RegistryItem);

            // The catalogue entry omits file contents (name/metadata only).
            indexItems.Add(new RegistryItem
            {
                Name = item.Name,
                Type = item.Type,
                Title = item.Title,
                Description = item.Description,
                NugetDependencies = item.NugetDependencies,
                RegistryDependencies = item.RegistryDependencies,
            });
        }

        var index = new RegistryIndex { Name = manifest.Name, Items = indexItems };
        await using (var indexStream = File.Create(Path.Combine(outputDir, "index.json")))
            await JsonSerializer.SerializeAsync(indexStream, index, CoreJson.Default.RegistryIndex);

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(index, CoreJson.Default.RegistryIndex));
            return 0;
        }

        settings.Line($"[green]Built[/] {indexItems.Count} item(s) to {Markup.Escape(outputDir)}.");
        return 0;
    }
}
