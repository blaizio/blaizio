using System.ComponentModel;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>remove</c>.</summary>
public sealed class RemoveSettings : ConfirmRegistrySettings
{
    /// <summary>Installed component names to remove.</summary>
    [CommandArgument(0, "<components...>")]
    [Description("Installed component names to remove")]
    public string[] Components { get; init; } = [];

    /// <summary>Remove even when another installed component depends on the item.</summary>
    [CommandOption("-f|--force")]
    [Description("Remove even when another installed component depends on it")]
    public bool Force { get; init; }

    /// <summary>Report what would be removed without touching anything.</summary>
    [CommandOption("--dry-run")]
    [Description("Preview what would be removed without touching anything")]
    public bool DryRun { get; init; }
}

/// <summary>
/// Removes individual components by record: exactly the files <c>add</c> wrote for each named item,
/// plus its <c>blaizio.json</c> entry. The wiring, packages and configuration stay - tearing all of
/// that down is <c>uninstall</c>'s job.
/// </summary>
public sealed class RemoveCommand : AsyncCommand<RemoveSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, RemoveSettings settings)
    {
        var cwd = settings.ResolvedCwd;
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(cwd, settings.Registry, ct);
        var service = new RemoveService(services.Registry);

        var request = new RemoveRequest
        {
            Components = settings.Components,
            Force = settings.Force,
            DryRun = true,
        };

        // Plan first, exactly like uninstall: the same pass powers the confirmation, --dry-run and
        // the "nothing to do" exit.
        var plan = await service.RunAsync(cwd, request, ct);

        if (plan.NothingToDo)
        {
            if (settings.Json)
                Console.Out.WriteLine(Payload(plan).ToJsonString());
            else
                foreach (var name in plan.NotInstalled)
                    settings.Line($"{Markup.Escape(name)} is not installed - nothing to remove.");
            return 0;
        }

        if (!settings.DryRun && !settings.NonInteractive)
        {
            Render(plan, settings);
            var names = string.Join(", ", plan.Items);
            if (plan.Items.Count > 0 && !AnsiConsole.Confirm(
                    $"Remove {Markup.Escape(names)}? [grey](tracked files only)[/]",
                    defaultValue: false))
            {
                settings.Warn("[yellow]Remove cancelled.[/]");
                return 0;
            }
        }

        var result = settings.DryRun
            ? plan
            : await service.RunAsync(cwd, new RemoveRequest
            {
                Components = settings.Components,
                Force = settings.Force,
                DryRun = false,
            }, ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(Payload(result).ToJsonString());
            return result.Blocked.Count > 0 || result.Unverifiable.Count > 0 ? 1 : 0;
        }

        if (settings.Silent)
            return result.Blocked.Count > 0 || result.Unverifiable.Count > 0 ? 1 : 0;

        // Interactive runs already saw the plan above; print it for -y / --dry-run runs.
        if (settings.DryRun || settings.NonInteractive)
            Render(result, settings);

        if (result.Items.Count > 0)
        {
            var verb = result.DryRun ? "Would remove" : "Removed";
            AnsiConsole.MarkupLine(
                $"[green]{verb}[/] {result.Items.Count} component(s), {result.Removed.Count} file(s). Packages and wiring untouched.");
        }

        if (result.Orphaned.Count > 0)
            AnsiConsole.MarkupLine(
                $"[grey]orphaned[/] {Markup.Escape(string.Join(", ", result.Orphaned))} - installed as dependencies, nothing needs them now.");

        if (result.UnusedPackages.Count > 0)
            AnsiConsole.MarkupLine(
                $"[grey]unused nuget[/] {Markup.Escape(string.Join(", ", result.UnusedPackages))} - no remaining component needs these; remove them yourself if your own code does not.");

        return result.Blocked.Count > 0 || result.Unverifiable.Count > 0 ? 1 : 0;
    }

    private static void Render(RemoveResult result, RemoveSettings settings)
    {
        if (settings.Silent)
            return;

        foreach (var name in result.NotInstalled)
            settings.Line($"{Markup.Escape(name)} is not installed - skipped.");

        foreach (var (item, dependents) in result.Blocked)
            CliOutput.Error.MarkupLine(
                $"  [red]![/] {Markup.Escape(item)} is required by {Markup.Escape(string.Join(", ", dependents))} - " +
                $"remove those first, or pass [white]--force[/].");

        foreach (var item in result.Unverifiable)
            CliOutput.Error.MarkupLine(
                $"  [red]![/] {Markup.Escape(item)} not removed: the registry is unreachable and this project's " +
                $"install record has no dependency data, so nothing can verify what depends on it. " +
                $"Retry online, or pass [white]--force[/] to remove anyway.");

        foreach (var file in result.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] [grey]removed[/] {Markup.Escape(file)}");
    }

    private static JsonObject Payload(RemoveResult result) => new()
    {
        ["items"] = new JsonArray([.. result.Items.Select(i => (JsonNode?)i)]),
        ["removed"] = new JsonArray([.. result.Removed.Select(f => (JsonNode?)f)]),
        ["notInstalled"] = new JsonArray([.. result.NotInstalled.Select(n => (JsonNode?)n)]),
        ["blocked"] = new JsonObject(result.Blocked.Select(entry =>
            new KeyValuePair<string, JsonNode?>(
                entry.Key,
                new JsonArray([.. entry.Value.Select(d => (JsonNode?)d)])))),
        ["unverifiable"] = new JsonArray([.. result.Unverifiable.Select(u => (JsonNode?)u)]),
        ["orphaned"] = new JsonArray([.. result.Orphaned.Select(o => (JsonNode?)o)]),
        ["unusedPackages"] = new JsonArray([.. result.UnusedPackages.Select(p => (JsonNode?)p)]),
        ["dryRun"] = result.DryRun,
    };
}
