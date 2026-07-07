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
    private static string ToolVersion =>
        typeof(InfoCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? typeof(InfoCommand).Assembly.GetName().Version?.ToString()
        ?? "?";

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var services = await CliServices.LoadAsync(settings.ResolvedCwd);
        var project = services.Project;
        var config = services.Config;

        if (settings.Json)
        {
            // Everything text mode shows, structured: tool + project facts, plus the config.
            var payload = new JsonObject
            {
                ["version"] = ToolVersion,
                ["projectDir"] = project.ProjectDir,
                ["csproj"] = project.CsprojPath,
                ["assembly"] = project.AssemblyName,
                ["rootNamespace"] = project.RootNamespace,
                ["initialized"] = config is not null,
                ["config"] = config is null
                    ? null
                    : JsonSerializer.SerializeToNode(config, CoreJson.Default.BlaizioConfig),
            };
            Console.Out.WriteLine(payload.ToJsonString());
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
        Row("initialized", config is not null ? "yes" : "no");
        if (config is not null)
        {
            Row("component namespace", config.Namespace);
            Row("output", config.Output);
            Row("theme", config.Theme);
            Row("registry", config.Registry);
            Row("rtl", config.Rtl ? "on" : "off");
        }

        AnsiConsole.Write(grid);
        return 0;
    }
}
