using Blaizio.Cli.Core.Registry;
using Spectre.Console;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// The registry reachability check commands run BEFORE they start changing a project. <c>add</c>
/// and <c>update</c> wire the project up first (packages, tokens file, host page) and fetch
/// components after, so an unreachable registry used to surface as a late error on a project that
/// had already been half-changed. Checking first turns that into a clean refusal that costs one
/// request.
/// </summary>
internal static class PreflightGate
{
    /// <summary>
    /// True when the registry answers. On failure the reason is already printed (with the
    /// not-live-yet hint for the default registry) and the caller should exit with
    /// <see cref="ExitCode"/> without touching anything.
    /// </summary>
    public static async Task<bool> RegistryReachableAsync(
        IRegistryClient registry,
        GlobalSettings settings,
        CancellationToken ct = default)
    {
        var status = await RegistryPreflight.CheckAsync(registry, ct);
        if (status.Reachable)
            return true;

        CliOutput.Error.MarkupLine($"[red]Registry error:[/] {Markup.Escape(status.Message ?? "the registry did not answer.")}");
        CliOutput.Error.MarkupLine("Nothing was changed - the registry was checked before any work started.");

        CliOutput.Error.MarkupLine(
            "Check the \"registry\" in blaizio.json, or pass [white]--registry <url|path>[/] on the command.");

        return false;
    }

    /// <summary>The exit code a failed preflight returns - the same code a registry error exits with.</summary>
    public const int ExitCode = 2;
}
