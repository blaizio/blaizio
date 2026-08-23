using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;

namespace Blaizio.Cli.Commands;

/// <summary>Renders an init run's outcome: the single JSON document or the human report with its
/// next-step hints. Presentation only - both shapes read the same <see cref="InitWiringResult"/>.</summary>
internal static class InitOutput
{
    public static int EmitJson(InitPlan plan, InitWiringResult run)
    {
        // The full outcome, not just the config: what was scaffolded, styled and added.
        var payload = new JsonObject
        {
            ["config"] = JsonSerializer.SerializeToNode(plan.Config, CoreJson.Default.BlaizioConfig),
            ["template"] = plan.Template?.ToString().ToLowerInvariant(),
            ["scaffold"] = run.Scaffold is null ? null : new JsonObject
            {
                ["written"] = new JsonArray([.. run.Scaffold.Written.Select(f => (JsonNode?)f)]),
                ["skipped"] = new JsonArray([.. run.Scaffold.Skipped.Select(f => (JsonNode?)f)]),
            },
            ["css"] = new JsonObject
            {
                ["input"] = run.Tailwind.InputPath,
                ["created"] = run.Tailwind.InputCreated,
                ["ejected"] = run.Tailwind.Ejected,
                ["skin"] = plan.Skin,
                ["preset"] = plan.Preset,
            },
            ["host"] = run.Host.HostPath is null ? null : new JsonObject
            {
                ["path"] = run.Host.HostPath,
                ["changes"] = new JsonArray([.. run.Host.Changes.Select(c => (JsonNode?)c)]),
            },
            ["services"] = run.Services.Path is null ? null : new JsonObject
            {
                ["path"] = run.Services.Path,
                ["registered"] = run.Services.Registered,
                ["changes"] = new JsonArray([.. run.Services.Changes.Select(c => (JsonNode?)c)]),
            },
            ["added"] = run.Added is null
                ? null
                : JsonSerializer.SerializeToNode(run.Added, CoreJson.Default.AddResult),
            ["csprojHardened"] = new JsonArray([.. run.Hardened.Select(h => (JsonNode?)h)]),
            ["packagesInstalled"] = run.PackagesInstalled,
            ["pipeline"] = run.Pipeline is null ? null : new JsonObject
            {
                ["id"] = run.Pipeline.PipelineId,
                ["changedFiles"] = new JsonArray([.. run.Pipeline.ChangedFiles.Select(f => (JsonNode?)f)]),
            },
        };
        Console.Out.WriteLine(payload.ToJsonString());
        return 0;
    }

    public static int Report(InitPlan plan, InitWiringResult run)
    {
        var ns = plan.Config.Namespace;
        AnsiConsole.MarkupLine($"[green]{(plan.TopUp ? "Refreshed" : "Initialized")}[/] {BlaizioConfig.FileName} (namespace [cyan]{Markup.Escape(ns)}[/]{(plan.Template is null ? "" : $", template [cyan]{plan.Template.ToString()!.ToLowerInvariant()}[/]")}).");
        if (run.Scaffold is { } scaffold)
            AnsiConsole.MarkupLine($"  [blue]scaffold[/] {scaffold.Written.Count} file(s){(scaffold.Skipped.Count > 0 ? $", {scaffold.Skipped.Count} skipped" : "")}");
        foreach (var change in run.Hardened)
            AnsiConsole.MarkupLine($"  [blue]csproj[/] {Markup.Escape(change)}");
        AnsiConsole.MarkupLine(run.Tailwind.Ejected
            ? $"  [blue]css[/] left {Markup.Escape(run.Tailwind.InputPath)} alone (ejected: the tokens file owns the contract)"
            : $"  [blue]css[/] {(run.Tailwind.InputCreated ? "created" : "updated")} {Markup.Escape(run.Tailwind.InputPath)} (skin [cyan]{Markup.Escape(plan.Skin)}[/], preset [cyan]{Markup.Escape(plan.Preset)}[/])");
        foreach (var change in run.Host.Changes)
            AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(run.Host.HostPath!)}: {Markup.Escape(change)}");
        foreach (var change in run.Services.Changes)
            AnsiConsole.MarkupLine($"  [blue]services[/] {Markup.Escape(run.Services.Path!)}: {Markup.Escape(change)}");
        if (run.Pipeline is not null)
        {
            foreach (var file in run.Pipeline.ChangedFiles)
                AnsiConsole.MarkupLine($"  [blue]tw[/] {Markup.Escape(file)}");
        }
        if (plan.Template is not null and not InitTemplate.Library && !plan.Scaffolded)
            // A caveat the user must see (the template they asked for isn't scaffolded) - yellow.
            AnsiConsole.MarkupLine("[yellow]Template scaffolding is not generated yet - config, packages and styling are ready.[/]");
        if (run.Added is not null)
            AnsiConsole.MarkupLine($"[green]Added[/] {run.Added.Items.Count} component(s).");

        // Next steps: the pipeline's build hint if one was wired, else the generic Tailwind command.
        var buildHint = run.Pipeline?.BuildHint
            ?? $"tailwindcss -i {(plan.Config.Css ?? "Styles/app.css").Replace('\\', '/')} -o wwwroot/app.css --watch";
        if (run.Pipeline is not null)
            foreach (var note in run.Pipeline.Notes)
                AnsiConsole.MarkupLine($"[grey]›[/] {Markup.Escape(note)}");

        if (plan.Scaffolded)
        {
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS ([white]{Markup.Escape(buildHint)}[/]) then [white]dotnet run[/].");
        }
        else if (run.Host.HostPath is not null)
        {
            // The host page was wired automatically - only the build step is left.
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS with [white]{Markup.Escape(buildHint)}[/] then run the app.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]Next:[/] compile CSS with [white]{Markup.Escape(buildHint)}[/] and reference the compiled css from your host page.");
        }
        // RTL support only readies the skins (logical properties); the page direction itself is
        // always the app's to set - init never stamps dir="rtl" on <html>.
        if (plan.Rtl)
            AnsiConsole.MarkupLine("      RTL: set [white]dir=\"rtl\"[/] on <html> (or wrap content in [white]<BzDirectionProvider Dir=\"Rtl\">[/]).");
        if (plan.Scrollbar)
            AnsiConsole.MarkupLine("      Scrollbars: the thin themed bar is on - its scrollbar-thin block in the tokens file is yours to restyle or delete.");
        return 0;
    }
}
