using Blaizio.Cli.Commands;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli;

/// <summary>
/// The command surface, factored out of <c>Program</c> so tests can host the exact same app
/// inside a <c>CommandAppTester</c>.
/// </summary>
internal static class CliApp
{
    /// <summary>Register every command, branch and the exception handler.</summary>
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("blaizio");

        // Runtime exceptions (registry down, no config, install failed) render as a clean one-liner;
        // set BLAIZIO_DEBUG=1 for the full trace. Parse/usage errors keep Spectre's own nice rendering.
        config.SetExceptionHandler((ex, _) =>
        {
            // Ctrl+C: quiet exit with the conventional 130.
            if (ex is OperationCanceledException)
            {
                CliOutput.Error.MarkupLine("[yellow]Cancelled.[/]");
                return 130;
            }

            // Errors go to stderr so a piped --json stdout stream stays clean.
            var label = ex is RegistryException ? "Registry error" : "Error";
            CliOutput.Error.MarkupLine($"[red]{label}:[/] {Spectre.Console.Markup.Escape(ex.Message)}");
            if (Environment.GetEnvironmentVariable("BLAIZIO_DEBUG") == "1")
                CliOutput.Error.WriteException(ex);
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
            .WithDescription("Compare installed components against the registry (exit 1 on drift).");
        config.AddCommand<UpdateCommand>("update")
            .WithDescription("Re-pull components, overwriting local copies (default: all installed).");
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
    }

    /// <summary>Normalize the documented <c>-ns</c> alias (Spectre reserves two-char shorts).</summary>
    public static string[] NormalizeNamespaceAlias(string[] input)
    {
        var copy = (string[])input.Clone();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i] == "--")
                break; // everything past the terminator is a literal argument
            if (copy[i] == "-ns")
                copy[i] = "--namespace";
        }
        return copy;
    }
}
