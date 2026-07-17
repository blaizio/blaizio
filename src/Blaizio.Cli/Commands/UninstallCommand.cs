using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>uninstall</c>.</summary>
public sealed class UninstallSettings : GlobalSettings
{
    /// <summary>Report what would be removed without touching anything.</summary>
    [CommandOption("--dry-run")]
    [Description("Preview what would be removed without touching anything.")]
    public bool DryRun { get; init; }
}

/// <summary>
/// Undoes what init/add put in, strictly by record: the components and NuGet packages tracked in
/// <c>blaizio.json</c>, their <c>@using</c> registrations, the managed CSS, the Tailwind targets,
/// the host-page wiring and the config itself. User-authored files and pre-existing package
/// references are never touched.
/// </summary>
public sealed class UninstallCommand : AsyncCommand<UninstallSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UninstallSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;
        var service = new UninstallService();

        // Plan first: it powers the "nothing to do" exit, the confirmation and --dry-run alike.
        var plan = await service.RunAsync(cwd, dryRun: true, ct);
        if (plan.NothingFound)
        {
            if (settings.Json)
                Console.Out.WriteLine(Payload(plan).ToJsonString());
            else
                // The command's ONLY output - default color, not grey (a dimmed line on a dark
                // terminal reads as decoration, and this is the answer the user asked for).
                settings.Line("Nothing to remove - no Blaizio configuration found.");
            return 0;
        }

        if (!settings.DryRun && !settings.NonInteractive)
        {
            Render(plan, settings);
            if (!AnsiConsole.Confirm(
                    "Remove everything Blaizio added? [grey](tracked components, packages and configuration)[/]",
                    defaultValue: false))
            {
                settings.Warn("[yellow]Uninstall cancelled.[/]");
                return 0;
            }
        }

        var result = settings.DryRun ? plan : await service.RunAsync(cwd, dryRun: false, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(Payload(result).ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        // Interactive runs already saw the plan above; print the list for -y / --dry-run runs.
        if (settings.DryRun || settings.NonInteractive)
            Render(result, settings);
        var verb = result.DryRun ? "Would remove" : "Removed";
        var packagesNote = result.Packages.Count > 0 ? $", {result.Packages.Count} package(s)" : "";
        AnsiConsole.MarkupLine(
            $"[green]{verb}[/] {result.Removed.Count} file(s), edited {result.Changed.Count}{packagesNote}. User-authored files untouched.");
        return 0;
    }

    private static void Render(UninstallResult result, UninstallSettings settings)
    {
        if (settings.Silent)
            return;
        foreach (var file in result.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] [grey]removed[/] {Markup.Escape(file)}");
        foreach (var file in result.Changed)
            AnsiConsole.MarkupLine($"  [yellow]~[/] [grey]edited[/]  {Markup.Escape(file)}");
        foreach (var id in result.Packages)
            AnsiConsole.MarkupLine($"  [red]-[/] [grey]nuget[/]   {Markup.Escape(id)}");
    }

    private static JsonObject Payload(UninstallResult result) => new()
    {
        ["removed"] = new JsonArray([.. result.Removed.Select(f => (JsonNode?)f)]),
        ["changed"] = new JsonArray([.. result.Changed.Select(f => (JsonNode?)f)]),
        ["packages"] = new JsonArray([.. result.Packages.Select(p => (JsonNode?)p)]),
        ["dryRun"] = result.DryRun,
    };
}
