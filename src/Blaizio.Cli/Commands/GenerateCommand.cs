using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry.Generation;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>generate</c> (maintainer command).</summary>
public sealed class GenerateSettings : GlobalSettings
{
    /// <summary>Source root to scan (the Blaizio.Ui project directory).</summary>
    [CommandArgument(0, "[SOURCE]")]
    [Description("Source root to scan (default: ./src/Blaizio.Ui).")]
    [DefaultValue("./src/Blaizio.Ui")]
    public string Source { get; init; } = "./src/Blaizio.Ui";

    /// <summary>Where to write the generated manifest.</summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Manifest output path (default: <source>/registry.json).")]
    public string? Output { get; init; }

    /// <summary>Registry display name.</summary>
    [CommandOption("--name <NAME>")]
    [Description("Registry display name.")]
    [DefaultValue("blaizio")]
    public string Name { get; init; } = "blaizio";
}

/// <summary>
/// Scans the Blaizio.Ui source tree into a registry manifest (one item per component family plus a
/// shared utils item), inferring cross-component dependencies. Writes <c>registry.json</c>, which
/// <c>build</c> then compiles into the hosted per-item JSON.
/// </summary>
public sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings)
    {
        var source = Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Source));
        if (!Directory.Exists(source))
        {
            AnsiConsole.MarkupLine($"[red]Source not found:[/] {Markup.Escape(source)}");
            return 1;
        }

        var output = settings.Output is not null
            ? Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Output))
            : Path.Combine(source, "registry.json");

        var generator = new RegistryGenerator(new GeneratorOptions { Name = settings.Name });
        var manifest = generator.Generate(source);

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using (var stream = File.Create(output))
            await JsonSerializer.SerializeAsync(stream, manifest, CoreJson.Default.RegistryIndex);

        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(manifest, CoreJson.Default.RegistryIndex));
            return 0;
        }

        var fileCount = manifest.Items.Sum(i => i.Files.Count);
        var withDeps = manifest.Items.Count(i => i.RegistryDependencies.Count > 0);
        AnsiConsole.MarkupLine($"[green]Generated[/] {manifest.Items.Count} item(s), {fileCount} file(s) → {Markup.Escape(output)}");
        AnsiConsole.MarkupLine($"[grey]{withDeps} item(s) have inferred registry dependencies. Run 'blaizio build {Markup.Escape(Path.GetFileName(output))}' to compile.[/]");
        return 0;
    }
}
