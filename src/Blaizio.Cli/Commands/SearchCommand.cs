using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>search</c> (and its deprecated <c>list</c> alias).</summary>
public sealed class SearchSettings : RegistrySettings
{
    /// <summary>Registries to search; empty falls back to the configured/overridden one.</summary>
    [CommandArgument(0, "[registries...]")]
    [Description("The registry addresses to search: @namespaces (from blaizio.json), URLs or local paths. When omitted, searches the registry configured in blaizio.json")]
    public string[] Registries { get; init; } = [];

    /// <summary>Case-insensitive substrings matched against item name/title/description.</summary>
    [CommandOption("-q|--query <query>")]
    [Description("Filter items by name, title or description. Comma-separated for multiple")]
    public string? Query { get; init; }

    /// <summary>Maximum items to show.</summary>
    [CommandOption("-l|--limit <number>")]
    [Description("Maximum number of items to display (default: 100)")]
    [DefaultValue(100)]
    public int Limit { get; init; } = 100;

    /// <summary>Items to skip from the start of the (filtered) list.</summary>
    // Long-only: -o means --output on every other command; a shorthand that flips meaning per
    // command is worse than none.
    [CommandOption("--offset <number>")]
    [Description("Number of items to skip (default: 0)")]
    [DefaultValue(0)]
    public int Offset { get; init; }
}

/// <summary>Searches registry items, optionally filtered by a query, across one or more registries.</summary>
public sealed class SearchCommand : AsyncCommand<SearchSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var ct = CliCancellation.Token;

        // Positional registries each get their own client; otherwise the configured/overridden
        // registry (--registry, else blaizio.json) is the single source. An `@namespace` source
        // resolves through the blaizio.json registries map (`registry add @ns=url`).
        var baseServices = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        string?[] sources = settings.Registries.Length > 0 ? [.. settings.Registries] : [settings.Registry];
        var items = new List<RegistryItem>();
        string? indexName = null;
        foreach (var source in sources)
        {
            // A namespace is searched through ITS OWN client, not a fresh one built from the
            // recorded URL: that is where the registry's credentials live, and a private registry
            // answers 401 to anything else.
            IRegistryClient client;
            if (source is ['@', ..])
            {
                client = baseServices.Registry is NamespacedRegistryClient namespaced
                    ? namespaced.For(source)
                    : throw new RegistryException(
                        $"Unknown registry '{source}'. Record it first: blaizio registry add {source}=<url>");
            }
            else
            {
                client = source == settings.Registry
                    ? baseServices.Registry
                    : (await CliServices.LoadAsync(settings.ResolvedCwd, source, ct)).Registry;
            }

            var index = await client.GetIndexAsync(ct);
            indexName ??= index.Name;
            items.AddRange(index.Items);
        }

        var offset = Math.Max(0, settings.Offset);
        var limit = Math.Max(0, settings.Limit);
        var filtered = FilterItems(items, settings.Query).ToArray();
        var page = filtered.Skip(offset).Take(limit).ToArray();

        if (settings.Json)
        {
            var payload = new RegistryIndex { Name = indexName ?? "search", Items = page };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CliJson.Default.RegistryIndex));
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (page.Length == 0)
        {
            settings.Line("[yellow]No matching components.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("[cyan]Name[/]");
        table.AddColumn("Type");
        table.AddColumn("Description");
        foreach (var item in page)
        {
            table.AddRow(
                $"[cyan]{Markup.Escape(item.Name)}[/]",
                Markup.Escape(item.Type.ToString().ToLowerInvariant()),
                Markup.Escape(item.Description ?? item.Title ?? string.Empty));
        }

        AnsiConsole.Write(table);
        var remaining = filtered.Length - offset - page.Length;
        if (remaining > 0)
            settings.Line($"{remaining} more - use --offset/--limit to page.");
        return 0;
    }

    /// <summary>
    /// Filter items by case-insensitive substrings over name/title/description. Comma separates
    /// alternatives — an item matching any of them stays.
    /// </summary>
    internal static IEnumerable<RegistryItem> FilterItems(IEnumerable<RegistryItem> items, string? query)
    {
        var terms = (query ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
            return items;

        return items.Where(i => terms.Any(term =>
            i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (i.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
    }
}
