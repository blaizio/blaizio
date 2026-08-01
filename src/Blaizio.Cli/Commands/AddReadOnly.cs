using System.Text.Json;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;

namespace Blaizio.Cli.Commands;

/// <summary>
/// The read-only <c>add</c> modes: <c>--diff</c> (compare against upstream, exit 1 on drift) and
/// <c>--view</c> (print registry file contents). Neither writes anything; both live here so the
/// command itself stays the write-path adapter.
/// </summary>
internal static class AddReadOnly
{
    /// <summary>
    /// <c>add --diff</c>: compare the requested components (default: all installed) against the
    /// registry without writing. The optional value filters to a single file path. Exit 1 on drift,
    /// like <c>git diff --exit-code</c>.
    /// </summary>
    public static async Task<int> ShowDiffAsync(
        CliServices services, BlaizioConfig config, AddSettings settings, CancellationToken ct)
    {
        if (settings.Components.Length == 0 && config.Installed.Count == 0)
        {
            settings.Warn("[yellow]No installed components recorded in blaizio.json.[/] Run [white]blaizio add <component>[/] first.");
            if (settings.Json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new DiffResult { Items = [] }, CliJson.Default.DiffResult));
            return 0;
        }

        var result = await new DiffService(services.Registry, services.Project, config)
            .RunAsync(settings.Components, ct);

        var pathFilter = settings.Diff.Value;
        bool Matches(string path) =>
            string.IsNullOrWhiteSpace(pathFilter) || path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase);

        var drift = false;
        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, CliJson.Default.DiffResult));
            return result.HasDrift ? 1 : 0;
        }

        foreach (var item in result.Items)
        {
            var files = item.Files.Where(f => f.Status is not DiffStatus.Unchanged && Matches(f.Path)).ToArray();
            if (files.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(pathFilter) && !settings.Silent)
                    AnsiConsole.MarkupLine($"  [green]=[/] [cyan]{Markup.Escape(item.Name)}[/] up to date");
                continue;
            }

            drift = true;
            if (settings.Silent)
                continue;
            AnsiConsole.MarkupLine($"  [yellow]~[/] [cyan]{Markup.Escape(item.Name)}[/]");
            foreach (var file in files)
            {
                var (glyph, color, label) = file.Status switch
                {
                    DiffStatus.Missing => ("-", "red", "missing"),
                    _ => ("~", "yellow", "changed"),
                };
                AnsiConsole.MarkupLine($"      [{color}]{glyph}[/] {Markup.Escape(file.Path)} [grey]({label})[/]");
            }
        }

        if (!settings.Silent)
            AnsiConsole.MarkupLine(drift
                ? "[yellow]Drift found.[/] Re-pull with [white]blaizio add <component> --overwrite[/] (overwrites local edits)."
                : "[green]Everything matches upstream.[/]");
        return drift ? 1 : 0;
    }

    /// <summary>
    /// <c>add --view</c>: print the requested components' registry files without writing. The
    /// optional value filters to a single file path.
    /// </summary>
    public static async Task<int> ShowFilesAsync(CliServices services, AddSettings settings, CancellationToken ct)
    {
        if (settings.Components.Length == 0)
        {
            settings.Warn("[yellow]Nothing to view - name a component:[/] [white]blaizio add <component> --view[/]");
            return 1;
        }

        var pathFilter = settings.View.Value;
        bool Matches(string path) =>
            string.IsNullOrWhiteSpace(pathFilter) || path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase);

        if (settings.Json)
        {
            var nodes = new System.Text.Json.Nodes.JsonArray();
            foreach (var reference in settings.Components)
            {
                var fetched = await services.Registry.GetItemAsync(reference, ct);
                nodes.Add(JsonSerializer.SerializeToNode(fetched, CliJson.Default.RegistryItem));
            }
            Console.Out.WriteLine(nodes.ToJsonString());
            return 0;
        }

        foreach (var reference in settings.Components)
        {
            var item = await services.Registry.GetItemAsync(reference, ct);
            if (settings.Silent)
                continue;

            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(item.Name)}[/]").LeftJustified());
            foreach (var file in item.Files.Where(f => Matches(f.Path)))
            {
                AnsiConsole.Write(new Rule($"[grey]{Markup.Escape(file.Path)}[/]").LeftJustified().RuleStyle("grey"));
                AnsiConsole.WriteLine(file.Content ?? "(no content)");
            }
        }

        return 0;
    }
}
