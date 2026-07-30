namespace Blaizio.Cli.Core.Projects;

/// <summary>Ensures a <c>@using</c> for the component namespace exists in the project's <c>_Imports.razor</c>.</summary>
internal static class ImportsUpdater
{
    /// <summary>
    /// Add <c>@using {namespace}</c> to <c>_Imports.razor</c> at the project root if absent.
    /// Creates the file when missing. Returns true when the file was changed.
    /// </summary>
    public static async Task<bool> EnsureUsingAsync(
        string projectDir,
        string componentNamespace,
        CancellationToken ct = default)
    {
        var path = Path.Combine(projectDir, "_Imports.razor");
        var directive = $"@using {componentNamespace}";

        var lines = File.Exists(path)
            ? (await File.ReadAllLinesAsync(path, ct)).ToList()
            : [];

        // `@using global::X` and `@using X` import the same namespace — don't append a duplicate.
        if (lines.Any(l => l.Trim() is var t &&
                (t == directive || t == $"@using global::{componentNamespace}")))
            return false;

        lines.Add(directive);
        await File.WriteAllLinesAsync(path, lines, ct);
        return true;
    }

    /// <summary>
    /// Remove <c>@using {namespace}</c> (plain or <c>global::</c>) from <c>_Imports.razor</c> —
    /// the inverse of <see cref="EnsureUsingAsync"/> for <c>uninstall</c>. Returns true when changed.
    /// </summary>
    public static async Task<bool> RemoveUsingAsync(
        string projectDir,
        string componentNamespace,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var path = Path.Combine(projectDir, "_Imports.razor");
        if (!File.Exists(path))
            return false;

        var lines = (await File.ReadAllLinesAsync(path, ct)).ToList();
        var kept = lines.Where(l => l.Trim() is var t &&
                t != $"@using {componentNamespace}" && t != $"@using global::{componentNamespace}")
            .ToList();
        if (kept.Count == lines.Count)
            return false;

        if (!dryRun)
            await File.WriteAllLinesAsync(path, kept, ct);
        return true;
    }
}
