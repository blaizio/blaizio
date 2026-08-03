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
    /// <summary>
    /// Every top-level command/branch name. Used both to build the app and to catch a command typed
    /// as a flag (<c>blaizio -update</c>), which would otherwise be a baffling parse error.
    /// </summary>
    // "help" stays out: `--help`/`-h` must reach Spectre as the built-in help flag, not be
    // "corrected" to the help command.
    private static readonly string[] CommandNames =
    [
        "new", "create", "apply", "add", "remove", "rm", "update", "docs", "search", "list", "view",
        "uninstall", "un", "eject", "info", "contrast", "generate", "build", "tailwind", "preset",
        "registry",
    ];

    /// <summary>Register every command, branch and the exception handler.</summary>
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("blaizio");
        config.SetApplicationVersion(ToolInfo.Version);
        config.SetHelpProvider(new BlaizioHelpProvider());
        // Spectre's default is relaxed parsing: an unknown option is silently collected into the
        // remaining args, so a typo (`init -t library`, `--overwite`) LOOKS accepted and does
        // nothing. Strict makes it a parse error with the usual usage rendering.
        config.Settings.StrictParsing = true;

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
            // The default registry is not deployed yet - a raw "could not reach" there is a trap.
            if (ex is RegistryException && ex.Message.Contains("blaiz.io", StringComparison.OrdinalIgnoreCase))
                CliOutput.Error.MarkupLine(
                    "The public blaiz.io registry is not live yet. Point the project at your registry: " +
                    "edit \"registry\" in blaizio.json, or pass [white]--registry <url|path>[/] on the command.");
            if (Environment.GetEnvironmentVariable("BLAIZIO_DEBUG") == "1")
                CliOutput.Error.WriteException(ex);
            return ex is RegistryException ? 2 : 1;
        });

        // Registration order is display order: the help provider lists commands as declared here.
        config.AddCommand<NewCommand>("new")
            .WithAlias("create")
            .WithDescription("Scaffold a new app from a template (showcase, webapp, wasm, library)");
        config.AddCommand<ApplyCommand>("apply")
            .WithDescription("Apply a preset to an existing project");
        config.AddCommand<AddCommand>("add")
            .WithDescription("Add components (and their dependencies), wiring Blaizio into the project first when needed");
        config.AddCommand<UpdateCommand>("update")
            .WithDescription("Update the Blaizio packages and re-pull installed components to this tool's versions (components you changed are asked about, and kept under -y)");
        config.AddCommand<DocsCommand>("docs")
            .WithDescription("Get docs, api references and usage examples for components");
        config.AddCommand<ViewCommand>("view")
            .WithDescription("Print a component's metadata and files without writing");
        config.AddCommand<SearchCommand>("search")
            .WithAlias("list") // the pre-rename name; scripts keep working
            .WithDescription("Search items from registries");
        config.AddCommand<RemoveCommand>("remove")
            .WithAlias("rm")
            .WithDescription("Remove installed components: delete their tracked files and drop them from the record");
        config.AddCommand<UninstallCommand>("uninstall")
            .WithAlias("un")
            .WithDescription("Undo the Blaizio wiring and adds: remove the tracked components, packages and configuration");
        config.AddCommand<EjectCommand>("eject")
            .WithDescription("Copy the contract sheets into your tokens file and own the styling plumbing");
        config.AddCommand<InfoCommand>("info")
            .WithDescription("Show project and configuration details");
        config.AddCommand<ContrastCommand>("contrast")
            .WithDescription("Audit the tokens file's colors for WCAG AA contrast (light + dark)");
        config.AddCommand<GenerateCommand>("generate")
            .WithDescription("Scan the Blaizio.Ui source tree into a registry.json manifest (maintainers)");
        config.AddCommand<BuildCommand>("build")
            .WithDescription("Build components for a blaizio registry");
        config.AddBranch("tailwind", tw =>
        {
            tw.SetDescription("Inspect or wire the Tailwind compile pipeline");
            tw.AddCommand<TailwindDetectCommand>("detect")
                .WithDescription("Report which Tailwind pipelines are present and which is recommended");
            tw.AddCommand<TailwindSetupCommand>("setup")
                .WithDescription("Wire a Tailwind pipeline (standalone, node, …) into the project");
            tw.AddCommand<TailwindFetchCommand>("fetch")
                .WithDescription("Fetch the standalone Tailwind binary");
        });
        config.AddBranch("preset", preset =>
        {
            preset.SetDescription("Manage presets");
            preset.AddCommand<PresetDecodeCommand>("decode")
                .WithDescription("Decode a preset code");
            preset.AddCommand<PresetResolveCommand>("resolve")
                .WithDescription("Resolve a preset from your project");
            preset.AddCommand<PresetUrlCommand>("url")
                .WithDescription("Get the create URL for a preset code");
            preset.AddCommand<PresetOpenCommand>("open")
                .WithDescription("Open a preset code in the browser");
        });
        config.AddBranch("registry", registry =>
        {
            registry.SetDescription("Manage registries");
            registry.AddCommand<RegistryAddCommand>("add")
                .WithDescription("Add registries to your project");
            registry.AddCommand<RegistryListCommand>("list")
                .WithDescription("Show the registries recorded in blaizio.json");
            registry.AddCommand<RegistryRemoveCommand>("remove")
                .WithAlias("rm")
                .WithDescription("Drop recorded registries (installed components stay)");
            registry.AddCommand<RegistryValidateCommand>("validate")
                .WithDescription("Validate a blaizio registry");
        });
        config.AddCommand<HelpCommand>("help")
            .WithDescription("Display help for command");

#if DEBUG
        config.ValidateExamples();
#endif
    }

    /// <summary>
    /// A command name typed as a flag at the command slot (<c>-update</c>, <c>--add</c>), or <c>null</c>
    /// when the first argument is a real command/option. Without this, such a token dies on an
    /// unrelated parse error. The command name (dashes stripped) is returned so the caller can suggest
    /// the correct form. <c>--help</c>/<c>--version</c> don't match a command, so they pass through
    /// untouched.
    /// </summary>
    public static string? DetectFlaggedCommand(string[] input)
    {
        // Only the first token matters - it's the command slot. Later dashed tokens are that
        // command's own options and belong to Spectre.
        var first = input.FirstOrDefault();
        if (first is null || first.Length == 0 || first[0] != '-')
            return null;

        var name = first.TrimStart('-');
        // Return the canonical name (not the raw input) so the suggestion is always correctly cased.
        return CommandNames.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
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
