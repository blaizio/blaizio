using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// Shared base for bundler-hosted Tailwind (Vite, PostCSS/webpack): these are detect-and-report only.
/// When a project already owns a bundler, the universal input we wrote is enough — the user adds the
/// Tailwind plugin to their existing config. Auto-editing arbitrary bundler configs is out of scope,
/// so <see cref="CanSetup"/> is false and setup surfaces the manual step instead of guessing.
/// </summary>
public abstract class BundlerPipeline : ITailwindPipeline
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public abstract string Summary { get; }

    /// <summary>The Tailwind plugin package for this bundler.</summary>
    protected abstract string Plugin { get; }

    /// <summary>Config file names that indicate this bundler is present.</summary>
    protected abstract IReadOnlyList<string> ConfigFiles { get; }

    /// <inheritdoc />
    public bool CanSetup => false;

    /// <inheritdoc />
    public Detection Detect(ProjectContext project)
    {
        var found = ConfigFiles.FirstOrDefault(f => File.Exists(Path.Combine(project.ProjectDir, f)));
        if (found is null)
            return Detection.Absent;

        var packageJson = Path.Combine(project.ProjectDir, "package.json");
        var wired = File.Exists(packageJson) &&
            File.ReadAllText(packageJson).Contains(Plugin, StringComparison.OrdinalIgnoreCase);
        return wired ? Detection.Present($"{found} + {Plugin}") : Detection.Partial($"{found} (no {Plugin} yet)");
    }

    /// <inheritdoc />
    public string BuildHint(ProjectContext project, TailwindPaths paths) => "run your bundler (e.g. npm run dev)";

    /// <inheritdoc />
    public Task<PipelineSetupResult> SetupAsync(ProjectContext project, TailwindPaths paths, CancellationToken ct = default)
        => throw new NotSupportedException(
            $"'{Id}' is detect-and-report only. Add {Plugin} to your bundler config, then import '{paths.Input}'.");
}

/// <summary>Vite-hosted Tailwind via <c>@tailwindcss/vite</c>.</summary>
public sealed class VitePipeline : BundlerPipeline
{
    /// <inheritdoc />
    public override string Id => "vite";

    /// <inheritdoc />
    public override string Title => "Vite plugin";

    /// <inheritdoc />
    public override string Summary => "App already uses Vite; add the @tailwindcss/vite plugin.";

    /// <inheritdoc />
    protected override string Plugin => "@tailwindcss/vite";

    /// <inheritdoc />
    protected override IReadOnlyList<string> ConfigFiles { get; } =
        ["vite.config.js", "vite.config.ts", "vite.config.mjs", "vite.config.cjs"];
}

/// <summary>
/// Rollup-hosted Tailwind. Rollup has no first-party Tailwind plugin — it hosts the
/// <c>@tailwindcss/postcss</c> plugin through its CSS handling (e.g. rollup-plugin-postcss), so
/// that's the package detection looks for alongside the rollup config.
/// </summary>
public sealed class RollupPipeline : BundlerPipeline
{
    /// <inheritdoc />
    public override string Id => "rollup";

    /// <inheritdoc />
    public override string Title => "Rollup plugin";

    /// <inheritdoc />
    public override string Summary => "App already uses Rollup; add @tailwindcss/postcss via its PostCSS plugin.";

    /// <inheritdoc />
    protected override string Plugin => "@tailwindcss/postcss";

    /// <inheritdoc />
    protected override IReadOnlyList<string> ConfigFiles { get; } =
        ["rollup.config.js", "rollup.config.mjs", "rollup.config.cjs", "rollup.config.ts"];
}

/// <summary>PostCSS-hosted Tailwind via <c>@tailwindcss/postcss</c> (webpack and friends).</summary>
public sealed class PostcssPipeline : BundlerPipeline
{
    /// <inheritdoc />
    public override string Id => "postcss";

    /// <inheritdoc />
    public override string Title => "PostCSS plugin";

    /// <inheritdoc />
    public override string Summary => "App uses a PostCSS bundler; add the @tailwindcss/postcss plugin.";

    /// <inheritdoc />
    protected override string Plugin => "@tailwindcss/postcss";

    /// <inheritdoc />
    protected override IReadOnlyList<string> ConfigFiles { get; } =
        ["postcss.config.js", "postcss.config.cjs", "postcss.config.mjs", "postcss.config.ts"];
}
