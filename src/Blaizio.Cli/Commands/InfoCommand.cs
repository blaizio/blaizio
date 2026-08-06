using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Prints project + configuration + tool details.</summary>
public sealed class InfoCommand : AsyncCommand<GlobalSettings>
{
    /// <summary>The tool's semantic version (e.g. <c>0.1.0-alpha.1</c>), not the 4-part assembly one.</summary>
    private static string ToolVersion => ToolInfo.Version;

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var services = await CliServices.LoadAsync(settings.ResolvedCwd);
        var project = services.Project;
        var config = services.Config;

        if (settings.Json)
        {
            Console.Out.WriteLine(BuildPayload(services).ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        void Row(string k, string v) => grid.AddRow($"[grey]{k}[/]", Markup.Escape(v));

        Row("version", ToolVersion);
        Row("project dir", project.ProjectDir);
        Row("csproj", project.CsprojPath ?? "(none)");
        Row("assembly", project.AssemblyName);
        Row("root namespace", project.RootNamespace);
        if (project.IsMaui)
            Row("host", "MAUI Blazor Hybrid (wwwroot/index.html)");
        Row("initialized", config is not null ? "yes" : "no");
        if (config is not null)
        {
            Row("component namespace", config.Namespace);
            Row("output", config.Output);
            Row("style", config.Style);
            Row("registry", config.Registry);
            Row("rtl", config.Rtl ? "on" : "off");
            if (config.Ejected)
                Row("ejected", "yes (the tokens file owns the contract)");
        }

        AnsiConsole.Write(grid);
        return 0;
    }

    /// <summary>
    /// Everything text mode shows, structured: tool + project facts, plus the config. Shared with
    /// the MCP server's <c>project_info</c> tool so both surfaces answer identically.
    /// </summary>
    internal static JsonObject BuildPayload(CliServices services)
    {
        var project = services.Project;
        var config = services.Config;
        return new JsonObject
        {
            ["version"] = ToolVersion,
            ["projectDir"] = project.ProjectDir,
            ["csproj"] = project.CsprojPath,
            ["assembly"] = project.AssemblyName,
            ["rootNamespace"] = project.RootNamespace,
            ["maui"] = project.IsMaui,
            ["initialized"] = config is not null,
            ["config"] = config is null
                ? null
                : JsonSerializer.SerializeToNode(config, CoreJson.Default.BlaizioConfig),
        };
    }
}
