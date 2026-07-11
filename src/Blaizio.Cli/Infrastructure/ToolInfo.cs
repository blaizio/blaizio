using System.Reflection;

namespace Blaizio.Cli.Infrastructure;

/// <summary>Facts about the tool itself.</summary>
internal static class ToolInfo
{
    /// <summary>The tool's semantic version (e.g. <c>0.1.0-alpha.1</c>), not the 4-part assembly one.</summary>
    public static string Version =>
        typeof(ToolInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? typeof(ToolInfo).Assembly.GetName().Version?.ToString()
        ?? "?";
}
