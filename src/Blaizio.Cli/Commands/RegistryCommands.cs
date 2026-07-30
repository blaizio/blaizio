using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>registry add</c>.</summary>
public sealed class RegistryAddSettings : CommandSettings
{
    /// <summary>The <c>@namespace=url</c> pairs to record.</summary>
    [CommandArgument(0, "[registries...]")]
    [Description("Registries to record, as @namespace=url pairs (e.g. @acme=https://acme.dev/r)")]
    public string[] Registries { get; init; } = [];

    /// <summary>Working directory holding the blaizio.json to update.</summary>
    [CommandOption("-c|--cwd <cwd>")]
    [Description("The working directory. Defaults to the current directory")]
    public string? Cwd { get; init; }

    /// <summary>Skip the trust confirmation prompt.</summary>
    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt (default: false)")]
    public bool Yes { get; init; }

    /// <summary>Suppress all non-essential output.</summary>
    [CommandOption("-s|--silent")]
    [Description("Mute output (default: false)")]
    public bool Silent { get; init; }
}

/// <summary>Records named registries (<c>@namespace</c> → URL) in <c>blaizio.json</c>.</summary>
public sealed class RegistryAddCommand : AsyncCommand<RegistryAddSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, RegistryAddSettings settings)
    {
        var ct = CliCancellation.Token;
        var cwd = Path.GetFullPath(settings.Cwd ?? Directory.GetCurrentDirectory());

        if (settings.Registries.Length == 0)
        {
            CliOutput.Error.MarkupLine(
                "[red]Error:[/] Nothing to add. Pass pairs like [white]blaizio registry add @acme=https://acme.dev/r[/]");
            return 1;
        }

        var config = await ConfigStore.LoadAsync(cwd, ct);
        if (config is null)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] No {BlaizioConfig.FileName} found in {Markup.Escape(cwd)}. Run [white]blaizio add[/] first.");
            return 1;
        }

        var added = new List<(string Ns, string Url)>();
        foreach (var entry in settings.Registries)
        {
            if (!TryParse(entry, out var ns, out var url, out var problem))
            {
                CliOutput.Error.MarkupLine($"[red]Error:[/] {problem}");
                return 1;
            }
            added.Add((ns, url));
        }

        // Trust gate: recording a registry means later installs from it copy source code into the
        // project and run `dotnet add package` for whatever its items declare. Say so once, here -
        // then confirm when a terminal is attached (skippable with -y; scripts proceed as-is).
        if (!settings.Silent)
            AnsiConsole.MarkupLine(
                "[yellow]Note:[/] items installed from a registry are source code compiled into your app, " +
                "plus the NuGet packages they declare. Only record registries you trust; " +
                "inspect items first with [white]blaizio view @ns/item[/].");
        if (!settings.Yes && !settings.Silent && AnsiConsole.Profile.Capabilities.Interactive)
        {
            foreach (var (ns, url) in added)
                AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(ns)}[/] → {Markup.Escape(url)}");
            if (!AnsiConsole.Confirm($"Record {(added.Count == 1 ? "this registry" : "these registries")}?"))
                return 0;
        }

        foreach (var (ns, url) in added)
            config.Registries[ns] = url;

        await ConfigStore.SaveAsync(cwd, config, ct);

        if (!settings.Silent)
        {
            foreach (var (ns, url) in added)
                AnsiConsole.MarkupLine($"  [green]+[/] [cyan]{Markup.Escape(ns)}[/] → {Markup.Escape(url)}");
            AnsiConsole.MarkupLine($"[green]Recorded[/] {added.Count} registr{(added.Count == 1 ? "y" : "ies")} in {BlaizioConfig.FileName}.");
        }
        return 0;
    }

    /// <summary>Parse one <c>@namespace=url</c> entry; the message explains what's wrong otherwise.</summary>
    internal static bool TryParse(string entry, out string ns, out string url, out string problem)
    {
        ns = url = problem = string.Empty;
        var eq = entry.IndexOf('=');

        if (!entry.StartsWith('@'))
        {
            problem = $"'{entry}' is missing its @namespace. Use @namespace=url (e.g. @acme=https://acme.dev/r).";
            return false;
        }
        if (eq < 0)
        {
            problem = $"'{entry}' has no URL. There is no public namespace directory yet - map it yourself with @namespace=url.";
            return false;
        }

        ns = entry[..eq].TrimEnd();
        url = entry[(eq + 1)..].Trim();
        if (ns.Length < 2)
        {
            problem = $"'{entry}' has an empty @namespace.";
            return false;
        }
        if (url.Length == 0
            || (!Uri.IsWellFormedUriString(url, UriKind.Absolute) && !Directory.Exists(url) && !Path.IsPathRooted(url)))
        {
            problem = $"'{entry}' has no usable URL or local path after '='.";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}

/// <summary>Settings for <c>registry validate</c>.</summary>
public sealed class RegistryValidateSettings : CommandSettings
{
    /// <summary>Path to the source manifest to validate.</summary>
    [CommandArgument(0, "[registry]")]
    [Description("Path to the source registry.json to validate (default: ./registry.json)")]
    [DefaultValue("./registry.json")]
    public string Manifest { get; init; } = "./registry.json";

    /// <summary>Working directory the manifest path resolves against.</summary>
    [CommandOption("-c|--cwd <cwd>")]
    [Description("The working directory. Defaults to the current directory")]
    public string? Cwd { get; init; }
}

/// <summary>
/// Validates a source <c>registry.json</c> the way <c>build</c> would consume it: parseable,
/// named, unique item names, every item shipping files, and every referenced source file present
/// on disk. Exit 1 on any finding.
/// </summary>
public sealed class RegistryValidateCommand : AsyncCommand<RegistryValidateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, RegistryValidateSettings settings)
    {
        var ct = CliCancellation.Token;
        var cwd = Path.GetFullPath(settings.Cwd ?? Directory.GetCurrentDirectory());
        var manifestPath = Path.GetFullPath(Path.Combine(cwd, settings.Manifest));

        if (!File.Exists(manifestPath))
        {
            CliOutput.Error.MarkupLine($"[red]Error:[/] Manifest not found: {Markup.Escape(manifestPath)}");
            return 1;
        }

        RegistryIndex manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync(stream, CoreJson.Default.RegistryIndex, ct)
                ?? throw new InvalidDataException("the file is empty");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            CliOutput.Error.MarkupLine($"[red]Error:[/] {Markup.Escape(manifestPath)} is not a valid registry.json: {Markup.Escape(ex.Message)}");
            return 1;
        }

        var problems = Validate(manifest, Path.GetDirectoryName(manifestPath)!);
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
                AnsiConsole.MarkupLine($"  [red]x[/] {Markup.Escape(problem)}");
            AnsiConsole.MarkupLine($"[red]Invalid:[/] {problems.Count} problem(s) in {Markup.Escape(settings.Manifest)}.");
            return 1;
        }

        AnsiConsole.MarkupLine(
            $"[green]Valid:[/] {manifest.Items.Count} item(s) in {Markup.Escape(settings.Manifest)} ([cyan]{Markup.Escape(manifest.Name ?? "")}[/]).");
        return 0;
    }

    /// <summary>Every structural problem in the manifest, ready to print one per line.</summary>
    internal static List<string> Validate(RegistryIndex manifest, string manifestDir)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.Name))
            problems.Add("registry has no \"name\".");
        if (manifest.Items.Count == 0)
            problems.Add("registry has no items.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                problems.Add("an item has no \"name\".");
                continue;
            }
            if (!names.Add(item.Name))
                problems.Add($"duplicate item name '{item.Name}'.");
            // Theme and font items are payload-only; everything else must carry files.
            switch (item.Type)
            {
                case ItemType.Theme when item.CssVars is not { IsEmpty: false }:
                    problems.Add($"theme '{item.Name}' has no cssVars (light/dark values).");
                    break;
                case ItemType.Font when item.Font is null:
                    problems.Add($"font '{item.Name}' has no font payload.");
                    break;
                case not (ItemType.Theme or ItemType.Font) when item.Files.Count == 0:
                    problems.Add($"'{item.Name}' ships no files.");
                    break;
            }

            foreach (var file in item.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    problems.Add($"'{item.Name}' has a file with no \"path\".");
                    continue;
                }
                // A source manifest points at on-disk files; inline content (a built item) passes.
                if (file.Content is null && !File.Exists(Path.GetFullPath(Path.Combine(manifestDir, file.Path))))
                    problems.Add($"'{item.Name}' references a missing file: {file.Path}");
            }
        }

        // Local registry dependencies must resolve inside this manifest; URLs and @namespaces are external.
        foreach (var item in manifest.Items)
        {
            foreach (var dep in item.RegistryDependencies)
            {
                var external = dep.StartsWith('@') || dep.Contains("://", StringComparison.Ordinal)
                    || dep.Contains('/') || dep.Contains('\\');
                if (!external && !names.Contains(dep))
                    problems.Add($"'{item.Name}' depends on unknown item '{dep}'.");
            }
        }

        return problems;
    }
}
