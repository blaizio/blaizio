namespace Blaizio.Cli.Core.Projects;

/// <summary>Ensures a <c>@using</c> for the component namespace exists in the project's <c>_Imports.razor</c>.</summary>
public static class ImportsUpdater
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
}
