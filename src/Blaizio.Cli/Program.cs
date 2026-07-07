using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Registry;
using Spectre.Console;
using Spectre.Console.Cli;

// `-ns` is a two-character short option, which Spectre's parser reserves for long options only.
// Normalize it to the canonical `--namespace` before parsing so the documented alias still works.
var cliArgs = NormalizeNamespaceAlias(args);

// No command + no flags => run `init` interactively (the solo TUI experience).
var app = new CommandApp<InitCommand>();
app.Configure(config =>
{
    config.SetApplicationName("blaizio");

    // Runtime exceptions (registry down, no config, install failed) render as a clean one-liner;
    // set BLAIZIO_DEBUG=1 for the full trace. Parse/usage errors keep Spectre's own nice rendering.
    config.SetExceptionHandler((ex, _) =>
    {
        var label = ex is RegistryException ? "Registry error" : "Error";
        AnsiConsole.MarkupLine($"[red]{label}:[/] {Markup.Escape(ex.Message)}");
        if (Debug()) AnsiConsole.WriteException(ex);
        return ex is RegistryException ? 2 : 1;
    });

    config.AddCommand<InitCommand>("init")
        .WithDescription("Initialize a project: write blaizio.json, install packages, add components.");
    config.AddCommand<AddCommand>("add")
        .WithDescription("Add components (and their dependencies) into the project.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List registry components.");
    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search registry components.");
    config.AddCommand<ViewCommand>("view")
        .WithDescription("Print a component's metadata and files without writing.");
    config.AddCommand<DiffCommand>("diff")
        .WithDescription("Show local vs upstream differences.");
    config.AddCommand<UpdateCommand>("update")
        .WithDescription("Re-pull components, overwriting local copies.");
    config.AddCommand<InfoCommand>("info")
        .WithDescription("Show project and configuration details.");
    config.AddCommand<GenerateCommand>("generate")
        .WithDescription("Scan the Blaizio.Ui source tree into a registry.json manifest (maintainers).");
    config.AddCommand<BuildCommand>("build")
        .WithDescription("Compile a source registry.json into resolved item JSON (maintainers).");
    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Run a codemod migration.");

    config.AddBranch("tailwind", tw =>
    {
        tw.SetDescription("Inspect or wire the Tailwind compile pipeline.");
        tw.AddCommand<TailwindDetectCommand>("detect")
            .WithDescription("Report which Tailwind pipelines are present and which is recommended.");
        tw.AddCommand<TailwindSetupCommand>("setup")
            .WithDescription("Wire a Tailwind pipeline (standalone, node, …) into the project.");
        tw.AddCommand<TailwindFetchCommand>("fetch")
            .WithDescription("Fetch the standalone Tailwind binary.");
    });

#if DEBUG
    config.ValidateExamples();
#endif
});

return await app.RunAsync(cliArgs);

static bool Debug() => Environment.GetEnvironmentVariable("BLAIZIO_DEBUG") == "1";

static string[] NormalizeNamespaceAlias(string[] input)
{
    var copy = (string[])input.Clone();
    for (var i = 0; i < copy.Length; i++)
        if (copy[i] == "-ns")
            copy[i] = "--namespace";
    return copy;
}
