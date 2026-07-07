using System.ComponentModel;
using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>view</c>.</summary>
public sealed class ViewSettings : GlobalSettings
{
    /// <summary>Item names or URLs to inspect.</summary>
    [CommandArgument(0, "<ITEMS>")]
    [Description("Item names or URLs to view.")]
    public string[] Items { get; init; } = [];
}

/// <summary>Prints a registry item's metadata and file contents without writing anything.</summary>
public sealed class ViewCommand : AsyncCommand<ViewSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ViewSettings settings)
    {
        var services = await CliServices.LoadAsync(settings.ResolvedCwd);

        foreach (var reference in settings.Items)
        {
            var item = await services.Registry.GetItemAsync(reference);

            if (settings.Json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(item, CoreJson.Default.RegistryItem));
                continue;
            }

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
