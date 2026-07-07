using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>list</c>/<c>search</c>: an optional query with paging.</summary>
public class ListSettings : GlobalSettings
{
    /// <summary>Case-insensitive substring matched against item name/title/description.</summary>
    [CommandOption("-q|--query <QUERY>")]
    [Description("Filter items by name, title or description.")]
    public string? Query { get; init; }

    /// <summary>Maximum items to show.</summary>
    [CommandOption("-l|--limit <N>")]
    [Description("Maximum number of items to return.")]
    [DefaultValue(100)]
    public int Limit { get; init; } = 100;

    /// <summary>Items to skip from the start of the (filtered) list.</summary>
    [CommandOption("--offset <N>")]
    [Description("Number of items to skip.")]
    [DefaultValue(0)]
    public int Offset { get; init; }
}

/// <summary>Lists registry items, optionally filtered by a query.</summary>
public sealed class ListCommand : AsyncCommand<ListSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var index = await services.Registry.GetIndexAsync(ct);

        var offset = Math.Max(0, settings.Offset);
        var limit = Math.Max(0, settings.Limit);
        var filtered = FilterItems(index.Items, settings.Query).ToArray();
        var items = filtered.Skip(offset).Take(limit).ToArray();

        if (settings.Json)
        {
            var payload = new RegistryIndex { Name = index.Name, Items = items };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CoreJson.Default.RegistryIndex));
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (items.Length == 0)
        {
            settings.Line("[yellow]No matching components.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("[cyan]Name[/]");
        table.AddColumn("Type");
        table.AddColumn("Description");
        foreach (var item in items)
        {
            table.AddRow(
                $"[cyan]{Markup.Escape(item.Name)}[/]",
                Markup.Escape(item.Type.ToString().ToLowerInvariant()),
                Markup.Escape(item.Description ?? item.Title ?? string.Empty));
        }

        AnsiConsole.Write(table);
        var remaining = filtered.Length - offset - items.Length;
        if (remaining > 0)
            settings.Line($"[grey]{remaining} more — use --offset/--limit to page.[/]");
        return 0;
    }

    /// <summary>Filter items by a case-insensitive substring over name/title/description.</summary>
    internal static IEnumerable<RegistryItem> FilterItems(IEnumerable<RegistryItem> items, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return items;

        return items.Where(i =>
            i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (i.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}
