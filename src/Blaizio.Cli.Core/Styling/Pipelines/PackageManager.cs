using Blaizio.Cli.Core.Projects;

namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>A JavaScript package manager, detected from a project's lockfile.</summary>
public enum PackageManager
{
    /// <summary>No lockfile found; npm is the safe default.</summary>
    Npm,
    Pnpm,
    Yarn,
    Bun,
}

/// <summary>Lockfile-based package-manager detection and the commands each one uses.</summary>
public static class PackageManagers
{
    /// <summary>Detect the package manager from lockfiles in <paramref name="projectDir"/>.</summary>
    public static PackageManager Detect(string projectDir)
    {
        if (File.Exists(Path.Combine(projectDir, "pnpm-lock.yaml"))) return PackageManager.Pnpm;
        if (File.Exists(Path.Combine(projectDir, "yarn.lock"))) return PackageManager.Yarn;
        if (File.Exists(Path.Combine(projectDir, "bun.lockb")) ||
            File.Exists(Path.Combine(projectDir, "bun.lock"))) return PackageManager.Bun;
        return PackageManager.Npm;
    }

    /// <summary>Parse a user-supplied name (<c>--pm</c>) into a <see cref="PackageManager"/>, or null.</summary>
    public static PackageManager? Parse(string? name) => name?.ToLowerInvariant() switch
    {
        "npm" => PackageManager.Npm,
        "pnpm" => PackageManager.Pnpm,
        "yarn" => PackageManager.Yarn,
        "bun" => PackageManager.Bun,
        _ => null,
    };

    /// <summary>The install-dev-dependency command for a package manager.</summary>
    public static string AddDevCommand(PackageManager pm, string package) => pm switch
    {
        PackageManager.Pnpm => $"pnpm add -D {package}",
        PackageManager.Yarn => $"yarn add -D {package}",
        PackageManager.Bun => $"bun add -d {package}",
        _ => $"npm install -D {package}",
    };

    /// <summary>The run-script command for a package manager.</summary>
    public static string RunCommand(PackageManager pm, string script) => pm switch
    {
        PackageManager.Pnpm => $"pnpm {script}",
        PackageManager.Yarn => $"yarn {script}",
        PackageManager.Bun => $"bun run {script}",
        _ => $"npm run {script}",
    };
}
