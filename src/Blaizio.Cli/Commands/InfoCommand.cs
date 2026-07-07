using System.Text.Json;
using Blaizio.Cli.Core;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Prints project + configuration + tool details.</summary>
public sealed class InfoCommand : AsyncCommand<GlobalSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        var services = await CliServices.LoadAsync(settings.ResolvedCwd);
        var project = services.Project;
        var config = services.Config;

        if (settings.Json)
        {
            if (config is not null)
                AnsiConsole.WriteLine(JsonSerializer.Serialize(config, CoreJson.Default.BlaizioConfig));
            else
                AnsiConsole.WriteLine("{}");
            return 0;
        }

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        void Row(string k, string v) => grid.AddRow($"[grey]{k}[/]", Markup.Escape(v));

        Row("version", typeof(InfoCommand).Assembly.GetName().Version?.ToString() ?? "?");
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
