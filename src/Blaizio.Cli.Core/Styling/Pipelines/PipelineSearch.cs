namespace Blaizio.Cli.Core.Styling.Pipelines;

/// <summary>
/// Where bundler/Node configs are searched for. A Blazor csproj rarely sits next to the JS
/// toolchain: rollup/vite/postcss configs live at the repo root in a monorepo, or in a
/// <c>lib/</c>-style child folder — detection that only probes the project directory silently
/// misses an owned bundler and lets <c>auto</c> wire standalone over it.
/// </summary>
internal static class PipelineSearch
{
    /// <summary>
    /// The directories to probe, nearest first: the project dir, its immediate children that hold
    /// a <c>package.json</c> (a <c>lib/</c> toolchain folder), then ancestors up to the repo root.
    /// Ancestors are only eligible when a repo marker (<c>.git</c>, a solution file) is found
    /// within a few levels — without one there is no "same repo" and a parent's config is someone
    /// else's (a temp dir, the user's home).
    /// </summary>
    public static IEnumerable<string> Roots(string projectDir)
    {
        yield return projectDir;

        foreach (var child in SafeChildren(projectDir))
        {
            var name = Path.GetFileName(child);
            if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules" or "wwwroot")
                continue;
            if (File.Exists(Path.Combine(child, "package.json")))
                yield return child;
        }

        // Collect up to 4 ancestors, then keep only the chain up to (and including) the repo root.
        var ancestors = new List<string>();
        var repoRootIndex = -1;
        var parent = Directory.GetParent(projectDir);
        for (var i = 0; i < 4 && parent is not null; i++, parent = parent.Parent)
        {
            ancestors.Add(parent.FullName);
            if (IsRepoRoot(parent.FullName))
            {
                repoRootIndex = i;
                break;
            }
        }

        if (repoRootIndex < 0)
            yield break;
        foreach (var ancestor in ancestors)
            yield return ancestor;
    }

    private static bool IsRepoRoot(string dir)
    {
        try
        {
            return Directory.Exists(Path.Combine(dir, ".git"))
                || Directory.EnumerateFiles(dir, "*.sln").Any()
                || Directory.EnumerateFiles(dir, "*.slnx").Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> SafeChildren(string dir)
    {
        try
        {
            return Directory.EnumerateDirectories(dir).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
