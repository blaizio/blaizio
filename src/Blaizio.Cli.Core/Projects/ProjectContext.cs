using System.Xml.Linq;

namespace Blaizio.Cli.Core.Projects;

/// <summary>
/// Locates and reads the single Blazor <c>.csproj</c> at a project root, exposing the facts the
/// CLI needs: its path, assembly name and root namespace (for defaulting the component namespace).
/// </summary>
public sealed class ProjectContext
{
    private ProjectContext(
        string projectDir,
        string? csprojPath,
        string assemblyName,
        string rootNamespace,
        string? sdk = null,
        bool hasAspNetCoreFrameworkRef = false)
    {
        ProjectDir = projectDir;
        CsprojPath = csprojPath;
        AssemblyName = assemblyName;
        RootNamespace = rootNamespace;
        Sdk = sdk;
        HasAspNetCoreFrameworkRef = hasAspNetCoreFrameworkRef;
    }

    /// <summary>The directory the command is operating on.</summary>
    public string ProjectDir { get; }

    /// <summary>Absolute path to the discovered <c>.csproj</c>, or null when none exists yet.</summary>
    public string? CsprojPath { get; }

    /// <summary>Assembly name (explicit or the csproj file name).</summary>
    public string AssemblyName { get; }

    /// <summary>Root namespace (explicit <c>RootNamespace</c> or the assembly name).</summary>
    public string RootNamespace { get; }

    /// <summary>The csproj's <c>Sdk</c> attribute (e.g. <c>Microsoft.NET.Sdk.Razor</c>), or null.</summary>
    public string? Sdk { get; }

    /// <summary>True when the project declares <c>&lt;FrameworkReference Include="Microsoft.AspNetCore.App" /&gt;</c>.</summary>
    public bool HasAspNetCoreFrameworkRef { get; }

    /// <summary>
    /// True when this is a bare class library (<c>Microsoft.NET.Sdk</c>) that can't compile Razor
    /// components as-is: no Razor SDK, and Blazor SDKs bring ASP.NET Core implicitly.
    /// </summary>
    public bool IsBareClassLibrary =>
        CsprojPath is not null && string.Equals(Sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase);

    /// <summary>The namespace to offer as the default for copied components.</summary>
    public string DefaultComponentNamespace => $"{RootNamespace}.Components.Ui";

    /// <summary>Discover the project at <paramref name="projectDir"/>.</summary>
    public static ProjectContext Discover(string projectDir)
    {
        var csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        var fallbackName = new DirectoryInfo(projectDir).Name;
        if (csproj is null)
            return new ProjectContext(projectDir, null, fallbackName, fallbackName);

        var assemblyName = Path.GetFileNameWithoutExtension(csproj);
        var rootNamespace = assemblyName;
        string? sdk = null;
        var hasAspNetRef = false;

        try
        {
            var doc = XDocument.Load(csproj);
            sdk = doc.Root?.Attribute("Sdk")?.Value?.Trim();

            var props = doc.Descendants("PropertyGroup").SelectMany(g => g.Elements());
            foreach (var prop in props)
            {
                if (prop.Name.LocalName == "AssemblyName" && !string.IsNullOrWhiteSpace(prop.Value))
                    assemblyName = prop.Value.Trim();
                else if (prop.Name.LocalName == "RootNamespace" && !string.IsNullOrWhiteSpace(prop.Value))
                    rootNamespace = prop.Value.Trim();
            }

            hasAspNetRef = doc.Descendants("FrameworkReference")
                .Any(e => string.Equals(
                    e.Attribute("Include")?.Value?.Trim(), "Microsoft.AspNetCore.App",
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Xml.XmlException)
        {
            // Malformed csproj: fall back to file-name-derived defaults rather than failing hard.
        }

        return new ProjectContext(projectDir, csproj, assemblyName, rootNamespace, sdk, hasAspNetRef);
    }
}
