using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>view</c>.</summary>
public sealed class ViewSettings : GlobalSettings
{
    /// <summary>Item names or URLs to inspect.</summary>
    [CommandArgument(0, "<items...>")]
    [Description("Item names or URLs to view")]
    public string[] Items { get; init; } = [];
}

/// <summary>Prints a registry item's metadata and file contents without writing anything.</summary>
public sealed class ViewCommand : AsyncCommand<ViewSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ViewSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);

        if (settings.Json)
        {
            // One JSON document (an array), not newline-delimited ones — consistent with every
            // other command's --json output.
            var nodes = new JsonArray();
            foreach (var reference in settings.Items)
            {
                var fetched = await services.Registry.GetItemAsync(reference, ct);
                nodes.Add(JsonSerializer.SerializeToNode(fetched, CoreJson.Default.RegistryItem));
            }
            Console.Out.WriteLine(nodes.ToJsonString());
            return 0;
        }

        foreach (var reference in settings.Items)
        {
            var item = await services.Registry.GetItemAsync(reference, ct);

            if (settings.Silent)
                continue;

            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(item.Name)}[/]").LeftJustified());
            if (item.Description is not null)
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(item.Description)}[/]");
            if (item.NugetDependencies.Count > 0)
                AnsiConsole.MarkupLine($"[grey]nuget:[/] {Markup.Escape(string.Join(", ", item.NugetDependencies))}");
            if (item.RegistryDependencies.Count > 0)
                AnsiConsole.MarkupLine($"[grey]deps:[/] {Markup.Escape(string.Join(", ", item.RegistryDependencies))}");

            foreach (var file in item.Files)
            {
                AnsiConsole.Write(new Rule($"[grey]{Markup.Escape(file.Path)}[/]").LeftJustified().RuleStyle("grey"));
                AnsiConsole.WriteLine(file.Content ?? "(no content)");
            }
        }

        return 0;
    }
}
