using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blaizio.Cli.Core.Projects;
using Blaizio.Cli.Core;
using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;
using Blaizio.Cli.Core.Styling;
using Blaizio.Cli.Core.Writing;
using Blaizio.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Blaizio.Cli.Commands;

/// <summary>Settings for <c>blaizio update</c>.</summary>
public sealed class UpdateSettings : ConfirmRegistrySettings
{
    /// <summary>Components to re-pull. Empty re-pulls everything recorded in blaizio.json.</summary>
    [CommandArgument(0, "[components...]")]
    [Description("Components to re-pull, replacing local copies you have not changed (default: all installed)")]
    public string[] Components { get; init; } = [];

    /// <summary>Resolve and report only; bump nothing, write nothing.</summary>
    [CommandOption("--dry-run")]
    [Description("Report what would change without writing or installing (default: false)")]
    public bool DryRun { get; init; }

    /// <summary>Replace components changed since install without asking. Without it, an
    /// interactive run picks and an unattended one (<c>-y</c>) keeps every local change.</summary>
    [CommandOption("-f|--force")]
    [Description("Replace components you changed without asking (default: false - your changes are kept)")]
    public bool Force { get; init; }
}

/// <summary>
/// <c>blaizio update</c>: brings the whole Blaizio stack up to the versions this tool ships, in
/// lockstep - bumps the base NuGet packages (Blaizio.Base, Blaizio.Icons, TailwindMerge.NET) to
/// the tool's pinned versions, then re-pulls components from the registry — the recorded skin's
/// inlined variants — overwriting local copies. One command, both halves, deliberately
/// inseparable: registry components lean on the package's JS/CSS assets, so syncing one without
/// the other ships a source/package skew (a component importing a dist/ module its older package
/// doesn't carry). The tokens file stays the user's — the contract plumbing version-tracks the
/// Blaizio.Base package (materialized into <c>.blaizio/</c> at build) — only the imports inside a
/// bundler-recorded input are re-synced. A host page that already loads <c>boot.js</c> counts as
/// wired and is skipped. A project still on the v1 <c>Styles/blaizio/</c> layout goes through the
/// confirm-gated migration first. The tool itself is NOT updated here - that is
/// <c>dotnet tool update -g Blaizio.Cli</c>, which the summary line points at.
/// </summary>
public sealed class UpdateCommand : ProjectCommand<UpdateSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteInProjectAsync(CommandContext context, UpdateSettings settings)
    {
        var ct = CliCancellation.Token;
        var services = await CliServices.LoadAsync(settings.ResolvedCwd, settings.Registry, ct);
        var config = services.RequireConfig();

        // v1 layout detected: migrate to v3 (confirm-gated; -y accepts) instead of half-updating.
        if (TailwindSetup.IsLegacyV1(settings.ResolvedCwd))
            return await MigrateAsync(context, settings, config, ct);

        // Preflight before the package bump: this command bumps NuGet first and re-pulls components
        // after, so an unreachable registry would leave the packages moved and the components on
        // their old version. Only skipped when there is nothing to re-pull anyway.
        if ((settings.Components.Length > 0 || config.Installed.Count > 0)
            && !await PreflightGate.RegistryReachableAsync(services.Registry, settings, ct))
            return PreflightGate.ExitCode;

        // 1. Bump the base packages to this tool's pinned versions.
        var packagesBumped = !settings.DryRun && await BumpPackagesAsync(settings, services, config, ct);
        if (settings.DryRun)
            settings.Line($"[grey]dry-run:[/] would pin {string.Join(", ", PackageVersions.BaseSet.Select(p => $"[cyan]{p.Id}[/] {p.Version}"))}");

        var components = settings.Components;
        if (components.Length == 0)
        {
            // No args: re-pull everything blaizio.json records as installed. A record carrying
            // its source (a file, a URL, a repository address) is re-pulled from there; a plain
            // key is a name on the default registry and a qualified one routes through its own. A
            // pinned item is re-requested at its pin - update means "current for the floating,
            // exact for the pinned" - and a pin only makes sense on a name.
            components = [.. config.Installed
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Value.Source ?? (kv.Value.Pin is { } pin ? $"{kv.Key}@{pin}" : kv.Key))];

            if (components.Length == 0)
                settings.Warn("[yellow]No installed components recorded in blaizio.json.[/] Run [white]blaizio add <component>[/] first.");
        }

        // 2. Re-pull against the (possibly newer) registry.
        //    NoNuget: the base set was just pinned; don't let item metadata float versions again.
        AddResult? updated = null;
        if (components.Length > 0)
        {
            var addService = new AddService(services.Registry, services.Project, config, services.Dotnet);
            var request = new AddRequest
            {
                Components = components,
                Overwrite = true,
                NoNuget = true,
                DryRun = settings.DryRun,
                // Re-pulling replaces local copies, so anything edited since install goes to the
                // picker first. --force takes upstream regardless; -y (and any other unattended
                // run) keeps the local version - a scheduled update must not eat someone's work.
                Force = settings.Force,
                ResolveConflicts = LocalEditPrompt.For(settings),
                // A whole-ledger run goes on past an entry nothing serves any more and reports it
                // at the end: one stale record must not hold every other component's refresh. A
                // named run does not - a name that is not found is a typo to fix, not a ledger to
                // route around.
                SkipMissing = settings.Components.Length == 0,
            };

            updated = await AddCommand.RunAsync(addService, request, settings, ct, "Re-pulling components...");
        }

        // 3. The tokens file is the user's and the contract sheets version-track the Blaizio.Base
        //    package. Only the imports inside a bundler-recorded input are kept in sync (paths can
        //    go stale when the output dir moves); the default flow's own file is never touched by
        //    an update. An ejected project owns the contract inline — re-syncing would reinsert
        //    the dead .blaizio/ imports.
        TailwindResult? tailwind = null;
        if (config.Css is not null && !config.Ejected && !settings.DryRun)
        {
            tailwind = await new TailwindSetup(new EmbeddedCssAssets()).EnsureAsync(
                settings.ResolvedCwd, config.Output, new TailwindOptions(Rtl: config.Rtl),
                config.Preset, topUpUserInput: false, cssInput: config.Css,
                chart: config.Chart ?? "default", radius: config.Radius ?? "default", ct: ct);
        }

        // The host page: once it loads boot.js it's wired and the app's to evolve - repatching
        // would re-guess hrefs it may have customized (fingerprinted links). Only an unwired host
        // is (re)wired here.
        var hostSetup = new HostPageSetup();
        var host = settings.DryRun || hostSetup.IsWired(settings.ResolvedCwd)
            ? new HostPageResult()
            : await hostSetup.EnsureAsync(settings.ResolvedCwd, ct: ct);

        // Program.cs is the app's: update only says when the registration is missing. Without it
        // every component that injects a service fails at render, and nothing else reports why.
        var servicesRegistered = new ServicesSetup().IsRegistered(settings.ResolvedCwd);

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
                ["servicesRegistered"] = servicesRegistered,
            };
            Console.Out.WriteLine(payload.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        if (packagesBumped)
            AnsiConsole.MarkupLine($"[green]Packages[/] pinned: {string.Join(", ", PackageVersions.BaseSet.Select(p => $"[cyan]{p.Id}[/] {p.Version}"))}");
        if (updated is not null)
        {
            // Files that actually moved - a re-pull where everything already matched upstream is
            // worth reporting as such, not as N files "updated".
            var changed = updated.Files.Count(f =>
                f.Action is WriteAction.Created or WriteAction.Overwritten or WriteAction.Planned);
            AnsiConsole.MarkupLine(
                $"[green]{(settings.DryRun ? "Would re-pull" : "Re-pulled")}[/] {updated.Items.Count} component(s), {changed} file(s) changed.");
            LocalEditPrompt.ReportKept(settings, updated, "blaizio update --force");
            foreach (var skipped in updated.Skipped)
                settings.Warn($"[yellow]Skipped[/] {Markup.Escape(LedgerMiss(skipped.Reference, skipped.Reason, config))}");
        }
        if (tailwind is not null)
            AnsiConsole.MarkupLine($"  [blue]css[/] synced imports in {Markup.Escape(tailwind.InputPath)}");
        foreach (var change in host.Changes)
            AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        if (!servicesRegistered)
            settings.Warn(
                "[yellow]Program.cs:[/] no AddBlaizio() call found. Add [white]builder.Services.AddBlaizio();[/] before the host is built - " +
                "components that take ICore or the dialog and toast services fail at render without it " +
                "(\"No registered service of type 'Blaizio.ICore'\").");
        // The one thing this command cannot do for itself, and the one the packages it just pinned
        // are versioned by - so it gets a tag and a blank line rather than trailing the results as
        // untagged prose, where it read as a footnote and was missed.
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Tool itself:[/] [white]dotnet tool update -g Blaizio.Cli[/]");
        return 0;
    }

    /// <summary>
    /// Pins the base NuGet packages (Blaizio.Base, Blaizio.Icons, TailwindMerge.NET) to this
    /// <summary>
    /// Explains a ledger entry that could not be re-pulled. Which fix applies depends on what the
    /// record says about where the item came from. One carrying a source was installed from a
    /// file, URL or repository that no longer serves it. One without was recorded before sources
    /// were tracked (or really is a default-registry name), so the default registry was asked -
    /// the wrong place for anything that came from a file, a URL or another registry - and the
    /// raw "file not found: <path>" says nothing about that.
    /// </summary>
    internal static string LedgerMiss(string reference, string failure, BlaizioConfig config)
    {
        var bare = ItemReference.TrySplitVersion(reference, out var unpinned, out _) ? unpinned : reference;
        var hit = config.Installed.FirstOrDefault(kv =>
            string.Equals(kv.Key, bare, StringComparison.OrdinalIgnoreCase)
            || string.Equals(kv.Value.Source, reference, StringComparison.OrdinalIgnoreCase));
        var name = hit.Key ?? bare;

        if (hit.Value?.Source is { } source)
        {
            return $"'{name}' was installed from {source}, which no longer serves it. " +
                   $"Re-add it from where it lives now (blaizio add <path-or-url>) and the record follows, " +
                   $"or drop it with blaizio remove {name} (this deletes the files it installed). " +
                   $"Original failure: {failure}";
        }

        return $"'{name}' is recorded in blaizio.json, but this project's registry does not serve it ({config.Registry}). " +
               $"If it was installed from a file or URL, re-add it the same way (blaizio add <path-or-url>): " +
               $"the record will carry that source from then on, and update will follow it. " +
               $"If it came from another registry, record that one and reinstall it namespaced: " +
               $"blaizio registry add \"@ns=<url>\" then blaizio add @ns/{name}. " +
               $"If it is gone for good, drop it with blaizio remove {name} (this deletes the files it installed). " +
               $"Original failure: {failure}";
    }

    /// <summary>
    /// tool's versions, ledgering any ids the bump introduces so uninstall can undo exactly them.
    /// Returns whether the bump ran (a project without a .csproj skips it with a warning).
    /// </summary>
    private static async Task<bool> BumpPackagesAsync(
        GlobalSettings settings, CliServices services, BlaizioConfig config, CancellationToken ct)
    {
        if (services.Project.CsprojPath is null)
        {
            settings.Warn("[yellow]No .csproj found - skipping the package bump.[/]");
            return false;
        }

        // Ledger the ids the bump introduces (pre-existing references are user-owned) so
        // uninstall can undo exactly them.
        var preExisting = PackageLedger.PreExisting(
            services.Project.CsprojPath, PackageVersions.BaseSet.Select(p => p.Id));

        async Task BumpAsync(IProgress<string>? progress)
        {
            var install = await services.Dotnet.AddPackagesAsync(PackageVersions.BaseSet, progress, ct);
            if (!install.Success)
                throw new InvalidOperationException(
                    $"'dotnet add package' failed:{Environment.NewLine}{install.ErrorText}");
        }

        if (settings.Json || settings.Silent)
            await BumpAsync(null);
        else
            await AnsiConsole.Status().StartAsync("Updating packages...",
                ctx => BumpAsync(new Progress<string>(msg => ctx.Status(Markup.Escape(msg)))));

        if (PackageLedger.Record(config, PackageVersions.BaseSet.Select(p => p.Id), preExisting))
            await ConfigStore.SaveAsync(settings.ResolvedCwd, config, ct);
        return true;
    }

    /// <summary>
    /// The v1 → v3 migration: re-install every ledgered component from the recorded skin's
    /// inlined registry variants, bump the base packages, compose the tokens file from the v1
    /// managed sheets (the user's values, preset/fonts/pointer folded in), delete
    /// <c>Styles/blaizio/</c>, gitignore <c>.blaizio/</c> and strip the dead
    /// <c>style-*</c>/<c>preset-*</c> host classes. Destructive to local component edits, so it
    /// is confirm-gated (<c>-y</c> accepts). The component re-install runs FIRST: if the registry
    /// is unreachable, the project stays fully v1 instead of half-migrated.
    /// </summary>
    private static async Task<int> MigrateAsync(
        CommandContext context, UpdateSettings settings, BlaizioConfig config, CancellationToken ct)
    {
        var cwd = settings.ResolvedCwd;
        var components = config.Installed.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (!settings.NonInteractive && !AnsiConsole.Confirm(
                $"Migrate this project to the v3 CSS layout? [yellow]This re-installs {components.Length} component(s) " +
                $"(overwriting local edits - commit or stash first), rewrites the Tailwind input and deletes Styles/blaizio/.[/]"))
        {
            settings.Warn("[yellow]Migration cancelled. The project stays on the v1 layout.[/]");
            return 0;
        }

        // 1. Components first (network can fail; the CSS stays v1 until this succeeds). The
        //    registry client already resolves them to the recorded skin's inlined variants.
        if (components.Length > 0)
        {
            var exit = await new AddCommand().ExecuteAsync(context, new AddSettings
            {
                Cwd = settings.Cwd,
                Yes = settings.Yes,
                // A --json update must stay a single clean document: run the add leg silently.
                Silent = settings.Silent || settings.Json,
                Json = false,
                Registry = settings.Registry,
                Components = components,
                Overwrite = true,
                // The migration confirm above already said this overwrites local edits, and a
                // half-migrated project (v1 components beside v3 ones) is worse than none. NOT
                // `Force`, which on add means "re-write blaizio.json" - the config is fine.
                ForceOverwrite = true,
            });
            if (exit != 0)
                return exit;
        }

        // 2. A v1 project's packages predate the v3 layout by definition - bring them along so
        //    the re-installed components don't lean on assets their package doesn't carry.
        var services = await CliServices.LoadAsync(cwd, settings.Registry, ct);
        await BumpPackagesAsync(settings, services, config, ct);

        // 3. The CSS leg: compose + rewrite + delete, then record what happened.
        var migration = await new TailwindSetup(new EmbeddedCssAssets())
            .MigrateAsync(cwd, config.Output, config.Preset, config.Css, ct);
        config.CssCreated |= migration.InputWasCliOwned;
        await ConfigStore.SaveAsync(cwd, config, ct);

        // 4. The style-*/preset-* classes on <html> are dead in v3 - EnsureAsync strips them
        //    (and only ever adds wiring that is missing, so a customized host stays intact).
        var host = await new HostPageSetup().EnsureAsync(cwd, ct: ct);

        if (settings.Json)
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["migrated"] = true,
                ["input"] = migration.InputPath,
                ["components"] = components.Length,
                ["removed"] = new JsonArray([.. migration.Removed.Select(r => (JsonNode)r)]),
            }.ToJsonString());
            return 0;
        }

        if (settings.Silent)
            return 0;

        AnsiConsole.MarkupLine($"[green]Migrated to the v3 CSS layout.[/]");
        AnsiConsole.MarkupLine($"  [blue]css[/] rewrote {Markup.Escape(migration.InputPath)} (your token values carried over)");
        if (components.Length > 0)
            AnsiConsole.MarkupLine($"  [blue]components[/] re-installed {components.Length} from skin [cyan]{Markup.Escape(config.Style)}[/]");
        foreach (var removed in migration.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(removed)}");
        foreach (var change in host.Changes)
            AnsiConsole.MarkupLine($"  [blue]host[/] {Markup.Escape(host.HostPath!)}: {Markup.Escape(change)}");
        AnsiConsole.MarkupLine("The contract sheets now materialize into .blaizio/ on 'dotnet build' (gitignored).");
        return 0;
    }
}
