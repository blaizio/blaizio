namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Finds the project's Tailwind input file(s) by content, not by name — the file can be called
/// anything (<c>tailwind.css</c>, <c>site.css</c>, <c>styles/main.css</c>). A candidate is any
/// <c>.css</c> importing <c>tailwindcss</c> (v4) or carrying <c>@tailwind</c> directives (v3),
/// excluding everything the CLI itself manages.
/// </summary>
public static class TailwindInputLocator
{
    private static readonly string[] SkipDirs =
        ["bin", "obj", "node_modules", ".git", ".blaizio", "wwwroot", "dist"];

    /// <summary>
    /// Candidate inputs under <paramref name="projectDir"/>, project-relative POSIX paths,
    /// shallowest first. The CLI-managed <c>Styles/app.css</c> and the managed assets under
    /// <c>Styles/blaizio/</c> are never candidates — the default (non-bundler) flow owns those.
    /// </summary>
    public static IReadOnlyList<string> Discover(string projectDir)
    {
        var results = new List<string>();
        var pending = new Stack<string>();
        pending.Push(projectDir);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(child);
                    if (name.StartsWith('.') || SkipDirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                        continue;
                    pending.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*.css"))
                {
                    var relative = Path.GetRelativePath(projectDir, file).Replace('\\', '/');
                    if (relative.StartsWith($"{TailwindSetup.StylesDir}/{TailwindSetup.ManagedDir}/", StringComparison.OrdinalIgnoreCase)
                        || relative.Equals($"{TailwindSetup.StylesDir}/{TailwindSetup.InputName}", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (IsTailwindInput(file))
                        results.Add(relative);
                }
            }
            catch (IOException) { /* unreadable dir - skip */ }
            catch (UnauthorizedAccessException) { /* unreadable dir - skip */ }
        }

        return [.. results.OrderBy(r => r.Count(c => c == '/')).ThenBy(r => r, StringComparer.OrdinalIgnoreCase)];
    }

    private static bool IsTailwindInput(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (info.Length > 1024 * 1024) // a compiled/vendored sheet, not an authored input
                return false;
            var text = File.ReadAllText(file);
            if (text.StartsWith(TailwindSetup.Marker, StringComparison.Ordinal))
                return false; // the CLI's own input, wherever it sits
            return text.Contains("@import \"tailwindcss", StringComparison.Ordinal)
                || text.Contains("@import 'tailwindcss", StringComparison.Ordinal)
                || text.Contains("@tailwind ", StringComparison.Ordinal);
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
}
