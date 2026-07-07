using System.Text;

namespace Blaizio.Cli.Core.Styling;

/// <summary>The outcome of wiring Tailwind into a project, for reporting.</summary>
public sealed class TailwindResult
{
    /// <summary>Tailwind input file (project-relative), e.g. <c>Styles/app.css</c>.</summary>
    public required string InputPath { get; init; }

    /// <summary>Managed asset files written under <c>Styles/blaizio/</c> (project-relative).</summary>
    public required IReadOnlyList<string> Assets { get; init; }

    /// <summary>The skin that was installed.</summary>
    public required string Skin { get; init; }

    /// <summary>True when the input file was created; false when an existing one was updated in place.</summary>
    public required bool InputCreated { get; init; }
}

/// <summary>
/// Wires Tailwind v4 into a consumer project: writes the managed CSS assets (theme tokens, base
/// contract, chosen skin) under <c>Styles/blaizio/</c> and generates or updates the Tailwind input
/// <c>Styles/app.css</c> so it imports them, scans the component output directory, and enables the
/// dark variant. Re-running is safe: managed assets are rewritten; an existing input is only topped
/// up with any missing <c>@import</c>/<c>@source</c> lines, never clobbered.
/// </summary>
public sealed class TailwindSetup(ICssAssetProvider assets)
{
    private const string StylesDir = "Styles";
    private const string ManagedDir = "blaizio";
    private const string InputName = "app.css";
    private const string Marker = "/* blaizio:managed */";

    /// <summary>Run the setup for <paramref name="projectDir"/>.</summary>
    /// <param name="projectDir">Project root.</param>
    /// <param name="componentOutput">Component output dir (project-relative), scanned by Tailwind.</param>
    /// <param name="skin">Skin to install (without the <c>style-</c> prefix).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TailwindResult> EnsureAsync(
        string projectDir,
        string componentOutput,
        string skin,
        CancellationToken ct = default)
    {
        var stylesAbs = Path.Combine(projectDir, StylesDir);
        var managedAbs = Path.Combine(stylesAbs, ManagedDir);
        Directory.CreateDirectory(managedAbs);

        // Managed assets: always (re)written so they track the installed tool version.
        var skinFile = $"style-{skin}.css";
        await WriteAsset(managedAbs, "theme.css", assets.GetThemeCss(), ct);
        await WriteAsset(managedAbs, "animate.css", assets.GetAnimateCss(), ct);
        await WriteAsset(managedAbs, "base.css", assets.GetBaseCss(), ct);
        await WriteAsset(managedAbs, skinFile, assets.GetSkinCss(skin), ct);

        // Drop any previously-installed skin so a skin change doesn't leave an orphan file behind.
        foreach (var stale in Directory.EnumerateFiles(managedAbs, "style-*.css")
                     .Where(p => !string.Equals(Path.GetFileName(p), skinFile, StringComparison.OrdinalIgnoreCase)))
            File.Delete(stale);

        // @source globs are relative to the input file (in Styles/); point them at the component dir.
        var sourceGlob = ToPosix(Path.GetRelativePath(stylesAbs, Path.Combine(projectDir, componentOutput)));

        var required = new[]
        {
            "@import \"tailwindcss\";",
            // tw-animate-css is vendored (below) so the Node-free standalone binary can resolve it.
            $"@import \"./{ManagedDir}/animate.css\";",
            $"@import \"./{ManagedDir}/theme.css\";",
            $"@import \"./{ManagedDir}/base.css\";",
            $"@import \"./{ManagedDir}/{skinFile}\" layer(components);",
            $"@source \"{sourceGlob}/**/*.razor\";",
            $"@source \"{sourceGlob}/**/*.cs\";",
        };

        var inputAbs = Path.Combine(stylesAbs, InputName);
        var created = !File.Exists(inputAbs);

        // Create fresh, or fully regenerate a file we own (marker present) so a skin change doesn't
        // leave a stale import. A user-authored file is only topped up with missing directives.
        var isManaged = created ||
            (await File.ReadAllTextAsync(inputAbs, ct)).StartsWith(Marker, StringComparison.Ordinal);
        if (isManaged)
            await File.WriteAllTextAsync(inputAbs, BuildInput(required), ct);
        else
            await TopUpInput(inputAbs, required, ct);

        return new TailwindResult
        {
            InputPath = ToPosix(Path.Combine(StylesDir, InputName)),
            Assets =
            [
                ToPosix(Path.Combine(StylesDir, ManagedDir, "theme.css")),
                ToPosix(Path.Combine(StylesDir, ManagedDir, "base.css")),
                ToPosix(Path.Combine(StylesDir, ManagedDir, skinFile)),
            ],
            Skin = skin,
            InputCreated = created,
        };
    }

    private static async Task WriteAsset(string dir, string name, string content, CancellationToken ct)
        => await File.WriteAllTextAsync(Path.Combine(dir, name), content, ct);

    private static string BuildInput(IReadOnlyList<string> required)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        sb.AppendLine("/* Tailwind v4 input for Blaizio. Compile with the Tailwind CLI, e.g.:");
        sb.AppendLine("   tailwindcss -i Styles/app.css -o wwwroot/app.css --watch");
        sb.AppendLine("   Put `.style-<skin>` (and optionally `.dark`) on <html> to activate the look. */");
        foreach (var line in required)
            sb.AppendLine(line);
        return sb.ToString();
    }

    /// <summary>Append any required directive not already present, preserving the user's file.</summary>
    private static async Task TopUpInput(string inputAbs, IReadOnlyList<string> required, CancellationToken ct)
    {
        var existing = await File.ReadAllTextAsync(inputAbs, ct);
        var missing = required.Where(line => !existing.Contains(line, StringComparison.Ordinal)).ToArray();
        if (missing.Length == 0)
            return;

        var sb = new StringBuilder(existing);
        if (!existing.EndsWith('\n'))
            sb.AppendLine();
        sb.AppendLine($"{Marker} (added)");
        foreach (var line in missing)
            sb.AppendLine(line);
        await File.WriteAllTextAsync(inputAbs, sb.ToString(), ct);
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
