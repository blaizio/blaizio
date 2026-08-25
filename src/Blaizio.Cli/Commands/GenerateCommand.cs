using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry.Generation;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>generate</c>.</summary>
public sealed class GenerateSettings : GlobalSettings
{
    /// <summary>Source root to scan: the folder holding Components/ (and any root helpers).</summary>
    [CommandArgument(0, "[source]")]
    [Description("Source root to scan - the folder that holds Components/ (default: the current directory).")]
    [DefaultValue(".")]
    public string Source { get; init; } = ".";

    /// <summary>Where to write the generated manifest.</summary>
    [CommandOption("-o|--output <path>")]
    [Description("Manifest output path (default: <source>/registry.json).")]
    public string? Output { get; init; }

    /// <summary>Registry display name.</summary>
    [CommandOption("--name <name>")]
    [Description("Registry display name.")]
    [DefaultValue("blaizio")]
    public string Name { get; init; } = "blaizio";

    /// <summary>Emit the font items that mirror the CLI's font catalog (the official registry only).</summary>
    [CommandOption("--fonts")]
    [Description("Also emit one body and one heading item per catalog webfont - the official registry's font items. Off for third-party registries.")]
    [DefaultValue(false)]
    public bool Fonts { get; init; }
}

/// <summary>
/// Scans a component source tree into a registry manifest (one item per folder under Components/,
/// plus a shared utils item when the root holds helpers), inferring cross-component dependencies
/// inside the tree. Writes <c>registry.json</c>, which <c>build</c> then compiles into the hosted
/// per-item JSON. References to components outside the tree are reported, not guessed.
/// </summary>
public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Source));
        if (!Directory.Exists(source))
        {
            settings.Warn($"[red]Source not found:[/] {Markup.Escape(source)}");
            return 1;
        }

        var output = settings.Output is not null
            ? Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Output))
            : Path.Combine(source, "registry.json");

        var generator = new RegistryGenerator(new GeneratorOptions { Name = settings.Name, IncludeFonts = settings.Fonts });
        var manifest = generator.Generate(source);

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using (var stream = File.Create(output))
            await JsonSerializer.SerializeAsync(stream, manifest, CoreJson.Default.RegistryIndex);

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(manifest, CliJson.Default.RegistryIndex));
            return 0;
        }

        var fileCount = manifest.Items.Sum(i => i.Files.Count);
        var withDeps = manifest.Items.Count(i => i.RegistryDependencies.Count > 0);
        settings.Line($"[green]Generated[/] {manifest.Items.Count} item(s), {fileCount} file(s) → {Markup.Escape(output)}");
        settings.Line($"{withDeps} item(s) have inferred registry dependencies. Run 'blaizio build {Markup.Escape(Path.GetFileName(output))}' to compile.");
        if (generator.UnresolvedReferences.Count > 0)
        {
            // Only the author knows which registry those come from - name them and show the shape
            // of the fix instead of guessing an item name that might not exist.
            var list = string.Join(", ", generator.UnresolvedReferences);
            settings.Line($"[yellow]Referenced but not in this tree:[/] {Markup.Escape(list)}. If they come from the official registry, add \"@default/<item>\" to that item's registryDependencies in {Markup.Escape(Path.GetFileName(output))}.");
        }
        return 0;
    }
}
