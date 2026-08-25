using Blaizio.Cli.Core.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Infrastructure;

/// <summary>How a command treats several discovered projects.</summary>
public enum ProjectFanout
{
    /// <summary>Run once per selected project (all of them by default): add, update, remove, info...</summary>
    Each,

    /// <summary>Run in exactly one project - the command answers a question whose answer does not
    /// change per project (search, view, docs), so repeating it would only repeat the output.</summary>
    One,
}

/// <summary>
/// Base for every command that operates on a project. Resolves WHICH project before the command
/// body runs: the working directory when it is a project (or an unwired one with a csproj), else
/// the projects found beneath it. One project found runs silently; several prompt for a selection,
/// all checked by default, and the body runs once per pick with a header and an end summary. Under
/// <c>-y</c> every project is taken; under <c>--json</c> several projects are refused with the
/// <c>-c</c> hint, because two JSON documents on one stdout is not JSON.
/// </summary>
public abstract class ProjectCommand<TSettings> : AsyncCommand<TSettings> where TSettings : GlobalSettings
{
    /// <summary>How this command spreads over several projects. Defaults to <see cref="ProjectFanout.Each"/>.</summary>
    protected virtual ProjectFanout Fanout => ProjectFanout.Each;

    /// <summary>The command body, run with <see cref="GlobalSettings.ResolvedCwd"/> pointing at one project.</summary>
    protected abstract Task<int> ExecuteInProjectAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Command-to-command forwarding entry (<c>add</c> runs <c>apply</c>, <c>update</c> runs
    /// <c>add</c>): the framework's own entry went protected, and another command is not kin.
    /// </summary>
    public Task<int> RunAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken = default)
        => ExecuteAsync(context, settings, cancellationToken);

    /// <inheritdoc />
    protected sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        var root = settings.ResolvedCwd;
        if (!Directory.Exists(root) || ProjectDiscovery.IsProjectRoot(root))
            return await ExecuteInProjectAsync(context, settings, cancellationToken);

        var projects = ProjectDiscovery.FindProjects(root);
        if (projects.Count == 0)
            return await ExecuteInProjectAsync(context, settings, cancellationToken); // the body reports "not a project" as it always did

        if (projects.Count == 1)
        {
            settings.Line($"[grey]project[/] {Markup.Escape(ProjectDiscovery.Label(root, projects[0]))}");
            settings.EnterProject(projects[0]);
            return await ExecuteInProjectAsync(context, settings, cancellationToken);
        }

        var selected = Select(settings, root, projects);
        if (selected.Count == 0)
            return 1;

        if (selected.Count == 1)
        {
            settings.Line($"[grey]project[/] {Markup.Escape(ProjectDiscovery.Label(root, selected[0]))}");
            settings.EnterProject(selected[0]);
            return await ExecuteInProjectAsync(context, settings, cancellationToken);
        }

        // Fan out. Each project is its own run with its own registry, ledger and prompts; one
        // failing does not stop the rest, and the exit code is the worst of them.
        var results = new List<(string Label, int Code)>();
        foreach (var project in selected)
        {
            var label = ProjectDiscovery.Label(root, project);
            settings.Line($"\n[bold]{Markup.Escape(label)}[/]");
            settings.EnterProject(project);
            int code;
            try
            {
                code = await ExecuteInProjectAsync(context, settings, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                settings.Warn($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                code = 1;
            }
            results.Add((label, code));
        }

        settings.Line("");
        foreach (var (label, code) in results)
        {
            settings.Line(code == 0
                ? $"  [green]+[/] {Markup.Escape(label)}"
                : $"  [red]x[/] {Markup.Escape(label)} [grey](exit {code})[/]");
        }
        return results.Max(r => r.Code);
    }

    private IReadOnlyList<string> Select(TSettings settings, string root, IReadOnlyList<string> projects)
    {
        var labels = projects.Select(p => ProjectDiscovery.Label(root, p)).ToArray();

        if (settings.Json)
        {
            // Several projects can't share one JSON stdout. Name them so the caller can pick.
            settings.Warn($"[red]Error:[/] {projects.Count} Blaizio projects under {Markup.Escape(root)}: {Markup.Escape(string.Join(", ", labels))}. Pass [white]-c <project>[/] to choose one.");
            return [];
        }

        if (Fanout == ProjectFanout.One)
        {
            if (settings.NonInteractive || !AnsiConsole.Profile.Capabilities.Interactive)
            {
                settings.Warn($"[red]Error:[/] {projects.Count} Blaizio projects under {Markup.Escape(root)}: {Markup.Escape(string.Join(", ", labels))}. Pass [white]-c <project>[/] to choose one.");
                return [];
            }
            var pick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Which project?")
                .AddChoices(labels));
            return [projects[Array.IndexOf(labels, pick)]];
        }

        // No terminal to ask (CI, a pipe) is the same answer as -y: every project.
        if (settings.NonInteractive || !AnsiConsole.Profile.Capabilities.Interactive)
        {
            settings.Line($"[grey]projects[/] {Markup.Escape(string.Join(", ", labels))}");
            return projects;
        }

        var picked = ComponentPrompts.MultiSelect(
            $"[bold]{projects.Count} Blaizio projects found.[/] Which ones? [grey](all are selected - enter to continue)[/]",
            labels,
            preselectAll: true);
        if (picked.Length == 0)
        {
            settings.Warn("[yellow]No project selected.[/]");
            return [];
        }
        return [.. picked.Select(l => projects[Array.IndexOf(labels, l)])];
    }
}
