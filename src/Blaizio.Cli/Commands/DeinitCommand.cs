using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>deinit</c>.</summary>
public sealed class DeinitSettings : GlobalSettings
{
    /// <summary>Report what would be removed without touching anything.</summary>
    [CommandOption("--dry-run")]
    [Description("Preview what would be removed without touching anything.")]
    public bool DryRun { get; init; }
}

/// <summary>
/// Removes the Blaizio configuration <c>init</c> wired in — <c>blaizio.json</c>, the managed CSS,
/// the Tailwind targets and the host-page wiring. Copied components, their <c>@using</c>
/// registrations and the NuGet packages stay untouched, so pages keep compiling.
/// </summary>
public sealed class DeinitCommand : AsyncCommand<DeinitSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, DeinitSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;
        var service = new DeinitService();

        // Plan first: it powers the "nothing to do" exit, the confirmation and --dry-run alike.
        var plan = await service.RunAsync(cwd, dryRun: true, ct);
        if (plan.NothingFound)
        {
            if (settings.Json)
                Console.Out.WriteLine(Payload(plan).ToJsonString());
            else
                settings.Line("[grey]Nothing to remove — no Blaizio configuration found.[/]");
            return 0;
        }

        if (!settings.DryRun && !settings.NonInteractive)
        {
            Render(plan, settings);
            if (!AnsiConsole.Confirm(
                    "Remove this Blaizio configuration? [grey](components, pages and packages stay)[/]",
                    defaultValue: false))
            {
                settings.Warn("[yellow]Deinit cancelled.[/]");
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
        AnsiConsole.MarkupLine(
            $"[green]{verb}[/] {result.Removed.Count} file(s), edited {result.Changed.Count}. Components, pages and packages untouched.");
        return 0;
    }

    private static void Render(DeinitResult result, DeinitSettings settings)
    {
        if (settings.Silent)
            return;
        foreach (var file in result.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(file)}");
        foreach (var file in result.Changed)
            AnsiConsole.MarkupLine($"  [yellow]~[/] {Markup.Escape(file)}");
    }

    private static JsonObject Payload(DeinitResult result) => new()
    {
        ["removed"] = new JsonArray([.. result.Removed.Select(f => (JsonNode?)f)]),
        ["changed"] = new JsonArray([.. result.Changed.Select(f => (JsonNode?)f)]),
        ["dryRun"] = result.DryRun,
    };
}
