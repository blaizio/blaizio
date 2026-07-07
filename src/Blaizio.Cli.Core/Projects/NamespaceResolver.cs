using Blaizio.Cli.Core.Configuration;

namespace Blaizio.Cli.Core.Projects;

/// <summary>
/// Applies the namespace precedence rule: <c>--namespace</c> flag &gt; <c>blaizio.json</c> &gt;
/// inferred project default. The interactive prompt (when nothing is supplied) is the CLI's job;
/// <see cref="Resolve"/> supplies the value to pre-fill or use non-interactively.
/// </summary>
public static class NamespaceResolver
{
    /// <summary>
    /// Resolve the namespace for copied components. Returns the first of: <paramref name="flag"/>,
    /// the config namespace, then <see cref="ProjectContext.DefaultComponentNamespace"/>.
    /// </summary>
    public static string Resolve(string? flag, BlaizioConfig? config, ProjectContext project)
    {
        if (!string.IsNullOrWhiteSpace(flag))
            return flag.Trim();
        if (!string.IsNullOrWhiteSpace(config?.Namespace))
            return config.Namespace.Trim();
        return project.DefaultComponentNamespace;
    }
}
