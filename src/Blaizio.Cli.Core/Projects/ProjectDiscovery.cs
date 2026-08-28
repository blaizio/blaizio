namespace Blaizio.Cli.Core.Projects;

/// <summary>
/// Finds the Blaizio projects under a folder: every directory that carries a <c>blaizio.json</c>.
/// Lets a command run from a solution root and fan out over the projects beneath it instead of
/// demanding a <c>cd</c> into each one - the multi-project repo where one project got updated
/// and the other silently stayed behind is the case this exists for.
/// </summary>
public static class ProjectDiscovery
{
    /// <summary>The project record's file name - its presence is what makes a folder a project.</summary>
    public const string ConfigFileName = "blaizio.json";

    // Folders that never hold a project of their own and are expensive to walk: build output,
    // package caches, VCS internals, and the CLI's own per-project folder.
    private static readonly HashSet<string> s_pruned = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".idea", ".blaizio", "artifacts", "TestResults", "packages",
    };

    /// <summary>True when <paramref name="dir"/> is itself a project: it carries <c>blaizio.json</c>.</summary>
    public static bool IsProject(string dir) => File.Exists(Path.Combine(dir, ConfigFileName));

    /// <summary>
    /// True when <paramref name="dir"/> consumes the Blaizio NuGet packages without being a wired
    /// project: no <c>blaizio.json</c>, but a top-level <c>.csproj</c> referencing a
    /// <c>Blaizio.*</c> package. A class library that only uses the base package looks like this -
    /// it has nothing for the registry half of a command, but leaving it out of <c>update</c>'s
    /// package bump is how a solution ends up with two Blaizio.Base versions.
    /// </summary>
    public static bool IsPackageConsumer(string dir)
    {
        if (IsProject(dir))
            return false;
        foreach (var csproj in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly))
        {
            string text;
            try
            {
                text = File.ReadAllText(csproj);
            }
            catch (IOException)
            {
                continue;
            }
            if (text.Contains("Include=\"Blaizio.", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when <paramref name="dir"/> should be treated as the project a command runs in, without
    /// looking any further: it carries <c>blaizio.json</c>, or a <c>.csproj</c> (a project that is
    /// not wired yet - <c>add</c> is how it gets wired, so it must keep resolving to the folder).
    /// </summary>
    public static bool IsProjectRoot(string dir) =>
        IsProject(dir) || Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).Any();

    /// <summary>
    /// Every project directory under <paramref name="root"/>, sorted by path. The walk does not
    /// descend into a project once found (its sub-folders are its own), nor into build output and
    /// caches. <paramref name="root"/> itself is not a candidate - callers check it first with
    /// <see cref="IsProjectRoot"/>. With <paramref name="includePackageConsumers"/>, folders that
    /// merely reference the Blaizio packages (see <see cref="IsPackageConsumer"/>) count too - the
    /// caller that asks for them (<c>update</c>) knows to run only its package leg there.
    /// </summary>
    public static IReadOnlyList<string> FindProjects(string root, bool includePackageConsumers = false)
    {
        var found = new List<string>();
        Walk(root, found, includePackageConsumers);
        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    private static void Walk(string dir, List<string> found, bool includePackageConsumers)
    {
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var child in children)
        {
            var name = Path.GetFileName(child);
            if (s_pruned.Contains(name) || name.StartsWith('.'))
                continue;
            if (IsProject(child) || (includePackageConsumers && IsPackageConsumer(child)))
            {
                found.Add(child);
                continue;
            }
            Walk(child, found, includePackageConsumers);
        }
    }

    /// <summary>
    /// The label a project shows in a list: its path relative to <paramref name="root"/> with
    /// forward slashes, so two projects read as <c>src/App</c> and <c>src/App.Docs</c> rather
    /// than two absolute paths that differ at the end.
    /// </summary>
    public static string Label(string root, string projectDir)
        => Path.GetRelativePath(root, projectDir).Replace('\\', '/');
}
