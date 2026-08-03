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
        bool hasAspNetCoreFrameworkRef = false,
        bool isMaui = false)
    {
        ProjectDir = projectDir;
        CsprojPath = csprojPath;
        AssemblyName = assemblyName;
        RootNamespace = rootNamespace;
        Sdk = sdk;
        HasAspNetCoreFrameworkRef = hasAspNetCoreFrameworkRef;
        IsMaui = isMaui;
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
    /// True for a .NET MAUI project - a Blazor Hybrid app hosts its Blazor UI in a
    /// <c>BlazorWebView</c>, so the wiring is the same as a WASM app's (<c>wwwroot/index.html</c>
    /// is the host page) but the packaging is not: <c>wwwroot</c> ships through a <c>MauiAsset</c>
    /// glob, so anything generated during the build has to be re-registered.
    /// </summary>
    public bool IsMaui { get; }

    /// <summary>
    /// True when this is a bare class library (<c>Microsoft.NET.Sdk</c>) that can't compile Razor
    /// components as-is: no Razor SDK, and Blazor SDKs bring ASP.NET Core implicitly.
    /// </summary>
    public bool IsBareClassLibrary =>
        CsprojPath is not null && string.Equals(Sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase);

    /// <summary>The namespace to offer as the default for copied components.</summary>
    public string DefaultComponentNamespace => $"{RootNamespace}.Components.Ui";

    /// <summary>
    /// True when any framework in a <c>TargetFramework(s)</c> value is a MAUI platform head
    /// (<c>net10.0-android</c>, <c>-ios</c>, <c>-maccatalyst</c>, <c>-tizen</c>). Windows heads are
    /// deliberately not on the list on their own - <c>net10.0-windows</c> is also a WPF/WinForms
    /// target, and a MAUI project always carries a mobile head or <c>UseMaui</c> beside it.
    /// </summary>
    private static bool IsMauiTargetFrameworks(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(tfm => tfm.Contains("-android", StringComparison.OrdinalIgnoreCase)
                || tfm.Contains("-ios", StringComparison.OrdinalIgnoreCase)
                || tfm.Contains("-maccatalyst", StringComparison.OrdinalIgnoreCase)
                || tfm.Contains("-tizen", StringComparison.OrdinalIgnoreCase));

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
        var isMaui = false;

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
                else if (prop.Name.LocalName is "UseMaui" or "UseMauiEssentials"
                    && bool.TryParse(prop.Value.Trim(), out var useMaui) && useMaui)
                    isMaui = true;
                else if (prop.Name.LocalName is "TargetFramework" or "TargetFrameworks"
                    && IsMauiTargetFrameworks(prop.Value))
                    isMaui = true;
            }

            hasAspNetRef = doc.Descendants("FrameworkReference")
                .Any(e => string.Equals(
                    e.Attribute("Include")?.Value?.Trim(), "Microsoft.AspNetCore.App",
                    StringComparison.OrdinalIgnoreCase));

            // A hand-rolled MAUI csproj can leave UseMaui to a Directory.Build.props; the package
            // reference is the other signal that always travels with the project itself.
            isMaui = isMaui || doc.Descendants("PackageReference")
                .Any(e => (e.Attribute("Include")?.Value?.Trim() ?? "")
                    .StartsWith("Microsoft.Maui.", StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Xml.XmlException)
        {
            // Malformed csproj: fall back to file-name-derived defaults rather than failing hard.
        }

        return new ProjectContext(projectDir, csproj, assemblyName, rootNamespace, sdk, hasAspNetRef, isMaui);
    }
}
