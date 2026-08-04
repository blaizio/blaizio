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

    /// <summary>Item types to keep.</summary>
    [CommandOption("-t|--type <types>")]
    [Description("Filter by item type: ui, lib, theme, font. Comma-separated for multiple")]
    public string? Type { get; init; }

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

/// <summary>
/// Searches registry items, optionally filtered by a query and type, across one or more
/// registries. The search travels to each registry as query parameters on the catalogue request;
/// a dynamic registry answers with a filtered page and a <c>pagination</c> object, which is the
/// signal to trust its result as-is, and a static one returns the whole catalogue for the local
/// filter below - so both kinds work, and neither needs to know which it is talking to.
/// </summary>
public sealed class SearchCommand : AsyncCommand<SearchSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var ct = CliCancellation.Token;
        var offset = Math.Max(0, settings.Offset);
        var limit = Math.Max(0, settings.Limit);
        var types = NormalizeTypes(settings.Type);

        // Positional registries each get their own client; otherwise the configured/overridden
        // registry (--registry, else blaizio.json) is the single source. An `@namespace` source
        // resolves through the blaizio.json registries map (`registry add @ns=url`).
        var baseServices = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        string?[] sources = settings.Registries.Length > 0 ? [.. settings.Registries] : [settings.Registry];

        // The page bounds are only delegated when ONE registry answers: across several, a page is
        // a slice of the MERGED list, which no single registry can compute.
        var single = sources.Length == 1;
        var search = new RegistrySearch(
            settings.Query, types,
            single ? limit : null,
            single ? offset : null);

        var items = new List<RegistryItem>();
        string? indexName = null;
        RegistryPagination? serverPage = null;
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

            var index = await client.SearchAsync(search, ct);
            indexName ??= index.Name;

            if (index.Pagination is { } pagination)
            {
                // The registry filtered. Its items are final - re-filtering here could drop
                // matches a smarter server made (fuzzy, synonyms) that plain substrings miss.
                items.AddRange(index.Items);
                if (single)
                    serverPage = pagination;
            }
            else
            {
                items.AddRange(FilterItems(index.Items, settings.Query, types));
            }
        }

        // A single server-paged answer is already the page; everything else pages locally.
        var page = serverPage is not null
            ? items.ToArray()
            : items.Skip(offset).Take(limit).ToArray();
        var total = serverPage?.Total ?? items.Count;

        if (settings.Json)
        {
            // The emitted document is itself the dynamic-search response shape, so whatever
            // consumes `search --json` handles a dynamic registry's answer for free.
            var payload = new RegistryIndex
            {
                Name = indexName ?? "search",
                Items = page,
                Pagination = serverPage ?? new RegistryPagination
                {
                    Total = total,
                    Offset = offset,
                    Limit = limit,
                    HasMore = total > offset + page.Length,
                },
            };
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
        var remaining = total - offset - page.Length;
        if (remaining > 0)
            settings.Line($"{remaining} more - use --offset/--limit to page.");
        return 0;
    }

    /// <summary>
    /// Filter items by case-insensitive substrings over name/title/description, and by type.
    /// Comma separates alternatives - an item matching any of them stays.
    /// </summary>
    internal static IEnumerable<RegistryItem> FilterItems(
        IEnumerable<RegistryItem> items, string? query, IReadOnlyList<string>? types = null)
    {
        if (types is { Count: > 0 })
            items = items.Where(i => types.Contains(WireType(i.Type), StringComparer.OrdinalIgnoreCase));

        var terms = (query ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
            return items;

        return items.Where(i => terms.Any(term =>
            i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (i.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
    }

    /// <summary>
    /// The wire names for a <c>--type</c> value, forgiving the prefix: <c>ui</c> and
    /// <c>registry:ui</c> are the same ask. Null when nothing was asked.
    /// </summary>
    internal static IReadOnlyList<string>? NormalizeTypes(string? type)
    {
        var values = (type ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.StartsWith("registry:", StringComparison.OrdinalIgnoreCase)
                ? t.ToLowerInvariant()
                : $"registry:{t.ToLowerInvariant()}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 0 ? null : values;
    }

    /// <summary>An item type's wire name. Every current member is its lowercased name.</summary>
    private static string WireType(ItemType type) => $"registry:{type.ToString().ToLowerInvariant()}";
}
