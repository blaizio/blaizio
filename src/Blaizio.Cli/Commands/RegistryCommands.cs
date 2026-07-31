using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>registry add</c>.</summary>
public sealed class RegistryAddSettings : ConfirmSettings
{
    /// <summary>The <c>@namespace=url</c> pairs to record.</summary>
    [CommandArgument(0, "[registries...]")]
    [Description("Registries to record, as @namespace=url pairs (e.g. @acme=https://acme.dev/r)")]
    public string[] Registries { get; init; } = [];
}

/// <summary>Records named registries (<c>@namespace</c> → URL) in <c>blaizio.json</c>.</summary>
public sealed class RegistryAddCommand : AsyncCommand<RegistryAddSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, RegistryAddSettings settings)
    {
        var ct = CliCancellation.Token;
        var cwd = settings.ResolvedCwd;

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
        settings.Line(
            "[yellow]Note:[/] items installed from a registry are source code compiled into your app, " +
            "plus the NuGet packages they declare. Only record registries you trust; " +
            "inspect items first with [white]blaizio view @ns/item[/].");
        if (!settings.NonInteractive && AnsiConsole.Profile.Capabilities.Interactive)
        {
            foreach (var (ns, url) in added)
                AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(ns)}[/] → {Markup.Escape(url)}");
            if (!AnsiConsole.Confirm($"Record {(added.Count == 1 ? "this registry" : "these registries")}?"))
                return 0;
        }

        foreach (var (ns, url) in added)
            config.Registries[ns] = url;

        await ConfigStore.SaveAsync(cwd, config, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["recorded"] = new JsonArray([.. added.Select(a => (JsonNode?)new JsonObject
                {
                    ["namespace"] = a.Ns,
                    ["url"] = a.Url,
                })]),
            }.ToJsonString());
            return 0;
        }

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

/// <summary>Prints the registries recorded in <c>blaizio.json</c>.</summary>
public sealed class RegistryListCommand : AsyncCommand<GlobalSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var ct = CliCancellation.Token;
        var cwd = settings.ResolvedCwd;
        var config = await ConfigStore.LoadAsync(cwd, ct);
        if (config is null)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] No {BlaizioConfig.FileName} found in {Markup.Escape(cwd)}. Run [white]blaizio add[/] first.");
            return 1;
        }

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["registries"] = new JsonObject(config.Registries
                    .OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new KeyValuePair<string, JsonNode?>(r.Key, r.Value))),
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (config.Registries.Count == 0)
        {
            AnsiConsole.MarkupLine("No registries recorded. Add one: [white]blaizio registry add @acme=https://acme.dev/r[/]");
            return 0;
        }

        foreach (var (ns, url) in config.Registries.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
            AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(ns)}[/] → {Markup.Escape(url)}");
        return 0;
    }
}

/// <summary>Settings for <c>registry remove</c>.</summary>
public sealed class RegistryRemoveSettings : GlobalSettings
{
    /// <summary>The <c>@namespace</c>s to drop from the record.</summary>
    [CommandArgument(0, "[namespaces...]")]
    [Description("Registries to drop, by @namespace (e.g. @acme)")]
    public string[] Namespaces { get; init; } = [];
}

/// <summary>
/// Drops recorded registries from <c>blaizio.json</c>. Installed components from that registry
/// stay in place - this forgets the source, it does not uninstall (that is <c>remove</c>).
/// </summary>
public sealed class RegistryRemoveCommand : AsyncCommand<RegistryRemoveSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, RegistryRemoveSettings settings)
    {
        var ct = CliCancellation.Token;
        var cwd = settings.ResolvedCwd;

        if (settings.Namespaces.Length == 0)
        {
            CliOutput.Error.MarkupLine(
                "[red]Error:[/] Nothing to remove. Pass namespaces like [white]blaizio registry remove @acme[/]");
            return 1;
        }

        var config = await ConfigStore.LoadAsync(cwd, ct);
        if (config is null)
        {
            CliOutput.Error.MarkupLine(
                $"[red]Error:[/] No {BlaizioConfig.FileName} found in {Markup.Escape(cwd)}. Run [white]blaizio add[/] first.");
            return 1;
        }

        var removed = new List<string>();
        var unknown = new List<string>();
        foreach (var ns in settings.Namespaces)
        {
            if (config.Registries.Remove(ns))
                removed.Add(ns);
            else
                unknown.Add(ns);
        }

        if (removed.Count > 0)
            await ConfigStore.SaveAsync(cwd, config, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["removed"] = new JsonArray([.. removed.Select(ns => (JsonNode?)ns)]),
                ["unknown"] = new JsonArray([.. unknown.Select(ns => (JsonNode?)ns)]),
            }.ToJsonString());
            return unknown.Count > 0 && removed.Count == 0 ? 1 : 0;
        }

        if (!settings.Silent)
        {
            foreach (var ns in removed)
                AnsiConsole.MarkupLine($"  [red]-[/] [cyan]{Markup.Escape(ns)}[/]");
            foreach (var ns in unknown)
                AnsiConsole.MarkupLine($"  [yellow]?[/] [cyan]{Markup.Escape(ns)}[/] was not recorded");
            if (removed.Count > 0)
                AnsiConsole.MarkupLine($"[green]Removed[/] {removed.Count} registr{(removed.Count == 1 ? "y" : "ies")} from {BlaizioConfig.FileName}. Installed components stay; [white]blaizio remove[/] uninstalls them.");
        }
        return unknown.Count > 0 && removed.Count == 0 ? 1 : 0;
    }
}

/// <summary>Settings for <c>registry validate</c>.</summary>
public sealed class RegistryValidateSettings : GlobalSettings
{
    /// <summary>Path to the source manifest to validate.</summary>
    // [manifest], not [registry]: the positional is a local file path, --registry is a URL.
    [CommandArgument(0, "[manifest]")]
    [Description("Path to the source registry.json to validate (default: ./registry.json)")]
    [DefaultValue("./registry.json")]
    public string Manifest { get; init; } = "./registry.json";
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
        var manifestPath = Path.GetFullPath(Path.Combine(settings.ResolvedCwd, settings.Manifest));

        // CI-shaped: under --json every outcome (missing, unparseable, invalid, valid) is one
        // document with a findings array, so a pipeline never has to scrape markup.
        if (!File.Exists(manifestPath))
        {
            if (settings.Json)
                return EmitJson(settings.Manifest, valid: false, items: 0, name: null,
                    [$"manifest not found: {manifestPath}"]);
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
            if (settings.Json)
                return EmitJson(settings.Manifest, valid: false, items: 0, name: null,
                    [$"not a valid registry.json: {ex.Message}"]);
            CliOutput.Error.MarkupLine($"[red]Error:[/] {Markup.Escape(manifestPath)} is not a valid registry.json: {Markup.Escape(ex.Message)}");
            return 1;
        }

        var problems = Validate(manifest, Path.GetDirectoryName(manifestPath)!);
        if (settings.Json)
            return EmitJson(settings.Manifest, problems.Count == 0, manifest.Items.Count, manifest.Name, problems);

        if (problems.Count > 0)
        {
            if (!settings.Silent)
            {
                foreach (var problem in problems)
                    AnsiConsole.MarkupLine($"  [red]x[/] {Markup.Escape(problem)}");
                AnsiConsole.MarkupLine($"[red]Invalid:[/] {problems.Count} problem(s) in {Markup.Escape(settings.Manifest)}.");
            }
            return 1;
        }

        settings.Line(
            $"[green]Valid:[/] {manifest.Items.Count} item(s) in {Markup.Escape(settings.Manifest)} ([cyan]{Markup.Escape(manifest.Name ?? "")}[/]).");
        return 0;
    }

    /// <summary>The <c>--json</c> document: manifest path, verdict and the findings array.</summary>
    private static int EmitJson(string manifest, bool valid, int items, string? name, IReadOnlyList<string> problems)
    {
        Console.Out.WriteLine(new JsonObject
        {
            ["manifest"] = manifest.Replace('\\', '/'),
            ["name"] = name,
            ["valid"] = valid,
            ["items"] = items,
            ["problems"] = new JsonArray([.. problems.Select(p => (JsonNode?)p)]),
        }.ToJsonString());
        return valid ? 0 : 1;
    }

    /// <summary>
    /// True when <paramref name="name"/> is a plain slug safe to embed in file names and paths:
    /// ASCII letters/digits plus '-', '_' and '.', and not made of dots alone. Anything else
    /// (path separators, rooted prefixes, empty) could redirect where a built item lands.
    /// </summary>
    internal static bool IsSafeItemName(string? name)
        => !string.IsNullOrEmpty(name)
            && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            && name.Trim('.').Length > 0;

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
            if (!IsSafeItemName(item.Name))
            {
                problems.Add($"item name '{item.Name}' is not a slug (letters, digits, '-', '_', '.').");
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
                if (file.Content is null)
                {
                    string full;
                    try
                    {
                        full = SafePath.Resolve(manifestDir, file.Path);
                    }
                    catch (InvalidOperationException)
                    {
                        problems.Add($"'{item.Name}' has a file path that escapes the manifest directory: {file.Path}");
                        continue;
                    }
                    if (!File.Exists(full))
                        problems.Add($"'{item.Name}' references a missing file: {file.Path}");
                }
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
