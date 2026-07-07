using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>upgrade</c>.</summary>
public sealed class UpgradeSettings : GlobalSettings;

/// <summary>
/// Brings the whole Blaizio stack up to the versions this tool ships: bumps the base NuGet
/// packages (Blaizio.Base, Blaizio.Icons, TailwindMerge.NET), then re-pulls every installed
/// component so source and packages stay in lockstep. <c>update</c> = source sync only;
/// <c>upgrade</c> = version bump + source sync.
/// </summary>
public sealed class UpgradeCommand : AsyncCommand<UpgradeSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpgradeSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        // 1. Bump the base packages to this tool's pinned versions.
        var packagesBumped = false;
        if (services.Project.CsprojPath is null)
        {
            settings.Warn("[yellow]No .csproj found — skipping the package bump.[/]");
        }
        else
        {
            async Task BumpAsync()
            {
                var install = await services.Dotnet.AddPackagesAsync(PackageVersions.BaseSet, ct);
                if (!install.Success)
                    throw new InvalidOperationException(
                        $"'dotnet add package' failed:{Environment.NewLine}{install.ErrorText}");
                packagesBumped = true;
            }

            if (settings.Json || settings.Silent)
                await BumpAsync();
            else
                await AnsiConsole.Status().StartAsync("Upgrading packages...", _ => BumpAsync());
        }

        // 2. Re-pull every installed component against the (possibly newer) registry.
        //    NoNuget: the base set was just pinned; don't let item metadata float versions again.
        string[] components = [.. config.Installed.Keys.Order(StringComparer.OrdinalIgnoreCase)];
        AddResult? updated = null;
        if (components.Length > 0)
        {
            var addService = new AddService(services.Registry, services.Project, config, services.Dotnet);
            var request = new AddRequest { Components = components, Overwrite = true, NoNuget = true };

            if (settings.Json || settings.Silent)
            {
                updated = await addService.RunAsync(request, ct: ct);
            }
            else
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Re-pulling components...", async ctx =>
                    {
                        var progress = new Progress<string>(msg => ctx.Status(Markup.Escape(msg)));
                        updated = await addService.RunAsync(request, progress, ct);
                    });
            }
        }

        if (settings.Json)
        {
            var payload = new JsonObject
            {
                ["packages"] = new JsonArray([.. PackageVersions.BaseSet.Select(p =>
                    (JsonNode?)new JsonObject { ["id"] = p.Id, ["version"] = p.Version })]),
                ["packagesBumped"] = packagesBumped,
                ["updated"] = updated is null
                    ? null
                    : JsonSerializer.SerializeToNode(updated, CoreJson.Default.AddResult),
            };
            Console.Out.WriteLine(payload.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (packagesBumped)
            AnsiConsole.MarkupLine($"[green]Packages[/] pinned: {string.Join(", ", PackageVersions.BaseSet.Select(p => $"[cyan]{p.Id}[/] {p.Version}"))}");
        if (updated is not null)
            AnsiConsole.MarkupLine($"[green]Re-pulled[/] {updated.Items.Count} component(s), {updated.Files.Count} file(s).");
        else
            AnsiConsole.MarkupLine("[grey]No installed components recorded — nothing to re-pull.[/]");
        AnsiConsole.MarkupLine("[grey]Tool itself: [white]dotnet tool update -g Blaizio.Cli[/] (or your local manifest).[/]");
        return 0;
    }
}
