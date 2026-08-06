using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>docs</c>.</summary>
public sealed class DocsSettings : RegistrySettings
{
    /// <summary>Component names to document.</summary>
    [CommandArgument(0, "<components...>")]
    [Description("Component names")]
    public string[] Components { get; init; } = [];
}

/// <summary>One component parameter surfaced as API reference.</summary>
/// <param name="Name">Property name.</param>
/// <param name="Type">Declared C# type.</param>
/// <param name="Default">Initializer text, when the source sets one.</param>
/// <param name="Required">True when marked <c>[EditorRequired]</c>.</param>
/// <param name="File">Registry file the parameter was found in.</param>
public sealed record DocsParameter(string Name, string Type, string? Default, bool Required, string File);

/// <summary>
/// Prints docs pointers, the API reference and usage hints for components: the item's metadata,
/// its docs page URL, and every <c>[Parameter]</c> parsed straight out of the registry sources —
/// so agents and IDE plugins get the API without scraping the website.
/// </summary>
public sealed class DocsCommand : AsyncCommand<DocsSettings>
{
    /// <summary>The component's page on the docs site.</summary>
    private static string DocsUrl(string name) => $"https://blaiz.io/docs/components/{name}";

    /// <summary>
    /// A <c>[Parameter]</c>/<c>[EditorRequired]</c> property declaration in razor/cs source. The
    /// attribute char right after <c>[</c> being <c>P</c> keeps <c>[CascadingParameter]</c> out.
    /// </summary>
    private static readonly Regex ParameterRx = new(
        @"\[(?:EditorRequired\]\s*\[)?Parameter[^\]]*\]\s*(?<required>\[EditorRequired\]\s*)?" +
        @"public\s+(?<type>[\w\.\?<>,\[\]\s]+?)\s+(?<name>\w+)\s*" +
        @"\{[^{}]*\}\s*(?:=\s*(?<default>[^;\r\n]+);)?",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, DocsSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);

        if (settings.Json)
        {
            var nodes = new JsonArray();
            foreach (var reference in settings.Components)
                nodes.Add(ToJson(await services.Registry.GetItemAsync(reference, ct)));
            Console.Out.WriteLine(nodes.ToJsonString());
            return 0;
        }

        foreach (var reference in settings.Components)
        {
            var item = await services.Registry.GetItemAsync(reference, ct);

            if (settings.Silent)
                continue;

            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(item.Title ?? item.Name)}[/]").LeftJustified());
            if (item.Description is not null)
                AnsiConsole.MarkupLine(Markup.Escape(item.Description));
            if (item.Author is not null)
                AnsiConsole.MarkupLine($"[grey]author[/] {Markup.Escape(item.Author)}");
            AnsiConsole.MarkupLine($"[grey]docs[/]   {Markup.Escape(DocsUrl(item.Name))}");
            if (item.Docs is not null)
                AnsiConsole.MarkupLine($"[grey]note[/]   {Markup.Escape(item.Docs)}");
            if (item.NugetDependencies.Count > 0)
                AnsiConsole.MarkupLine($"[grey]nuget[/]  {Markup.Escape(string.Join(", ", item.NugetDependencies))}");
            if (item.RegistryDependencies.Count > 0)
                AnsiConsole.MarkupLine($"[grey]deps[/]   {Markup.Escape(string.Join(", ", item.RegistryDependencies))}");

            var parameters = ExtractParameters(item);
            if (parameters.Count > 0)
            {
                AnsiConsole.MarkupLine("[grey]api[/]");
                var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
                table.AddColumn("[cyan]Parameter[/]");
                table.AddColumn("Type");
                table.AddColumn("Default");
                foreach (var p in parameters)
                {
                    table.AddRow(
                        $"[cyan]{Markup.Escape(p.Name)}[/]{(p.Required ? " [red]*[/]" : "")}",
                        Markup.Escape(p.Type),
                        Markup.Escape(p.Default ?? ""));
                }
                AnsiConsole.Write(table);
            }

            AnsiConsole.MarkupLine(
                $"[grey]usage[/]  examples live on the docs page above; [white]blaizio view {Markup.Escape(item.Name)}[/] prints the full source, [white]blaizio add {Markup.Escape(item.Name)}[/] installs it.");
        }

        return 0;
    }

    /// <summary>Every <c>[Parameter]</c> across the item's razor/cs sources, in declaration order.</summary>
    internal static List<DocsParameter> ExtractParameters(RegistryItem item)
    {
        var parameters = new List<DocsParameter>();
        foreach (var file in item.Files)
        {
            if (file.Content is null)
                continue;
            foreach (Match match in ParameterRx.Matches(file.Content))
            {
                parameters.Add(new DocsParameter(
                    match.Groups["name"].Value,
                    Regex.Replace(match.Groups["type"].Value, @"\s+", " ").Trim(),
                    match.Groups["default"].Success ? match.Groups["default"].Value.Trim() : null,
                    match.Groups["required"].Success || match.Value.Contains("[EditorRequired]", StringComparison.Ordinal),
                    file.Path));
            }
        }
        return parameters;
    }

    // Internal: the MCP server serves the same payload from its get_docs tool.
    internal static JsonObject ToJson(RegistryItem item) => new()
    {
        ["name"] = item.Name,
        ["title"] = item.Title,
        ["description"] = item.Description,
        ["author"] = item.Author,
        ["url"] = DocsUrl(item.Name),
        ["docs"] = item.Docs,
        ["categories"] = item.Categories is null ? null : new JsonArray([.. item.Categories.Select(c => (JsonNode?)c)]),
        ["nugetDependencies"] = new JsonArray([.. item.NugetDependencies.Select(d => (JsonNode?)d)]),
        ["registryDependencies"] = new JsonArray([.. item.RegistryDependencies.Select(d => (JsonNode?)d)]),
        ["parameters"] = new JsonArray([.. ExtractParameters(item).Select(p => (JsonNode?)new JsonObject
        {
            ["name"] = p.Name,
            ["type"] = p.Type,
            ["default"] = p.Default,
            ["required"] = p.Required,
            ["file"] = p.File,
        })]),
    };
}
