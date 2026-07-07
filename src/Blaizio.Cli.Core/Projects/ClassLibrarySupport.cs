namespace Blaizio.Cli.Core.Projects;

/// <summary>
/// Makes a bare class library (<c>Microsoft.NET.Sdk</c>) able to compile copied Blazor components:
/// swap to the Razor SDK, reference the ASP.NET Core shared framework, turn on implicit usings,
/// and seed <c>_Imports.razor</c> with the standard Blazor usings a <c>dotnet new blazor</c> app
/// ships but a class library doesn't.
/// </summary>
public static class ClassLibrarySupport
{
    /// <summary>The standard Blazor usings copied components assume are in scope.</summary>
    public static readonly string[] StandardUsings =
    [
        "System.Net.Http",
        "Microsoft.AspNetCore.Components.Forms",
        "Microsoft.AspNetCore.Components.Routing",
        "Microsoft.AspNetCore.Components.Web",
    ];

    /// <summary>
    /// Patch the csproj of a bare class library. Text-level, format-preserving edits: the Sdk
    /// attribute is swapped in place, and missing properties/items are appended as new groups.
    /// Returns a change description per edit (empty when nothing was needed).
    /// </summary>
    public static async Task<IReadOnlyList<string>> HardenCsprojAsync(
        ProjectContext project,
        CancellationToken ct = default)
    {
        if (project.CsprojPath is null || !project.IsBareClassLibrary)
            return [];

        var changes = new List<string>();
        var text = await File.ReadAllTextAsync(project.CsprojPath, ct);

        // 1. Razor SDK — required to compile .razor files at all.
        const string bareSdk = "Sdk=\"Microsoft.NET.Sdk\"";
        const string razorSdk = "Sdk=\"Microsoft.NET.Sdk.Razor\"";
        var sdkIndex = text.IndexOf(bareSdk, StringComparison.Ordinal);
        if (sdkIndex >= 0)
        {
            text = string.Concat(text[..sdkIndex], razorSdk, text[(sdkIndex + bareSdk.Length)..]);
            changes.Add("Sdk → Microsoft.NET.Sdk.Razor");
        }

        var closeTag = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (closeTag < 0)
            return changes; // not a shape we can safely extend

        // 2. ASP.NET Core shared framework — Components/Web types live there; app SDKs bring it
        //    implicitly, a class library must opt in.
        if (!project.HasAspNetCoreFrameworkRef)
        {
            text = text.Insert(closeTag,
                """
                  <ItemGroup>
                    <FrameworkReference Include="Microsoft.AspNetCore.App" />
                  </ItemGroup>

                """);
            changes.Add("FrameworkReference Microsoft.AspNetCore.App");
            closeTag = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        }

        // 3. Implicit usings — copied .cs files assume System/Linq/Tasks are in scope.
        if (!text.Contains("<ImplicitUsings>", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Insert(closeTag,
                """
                  <PropertyGroup>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>

                """);
            changes.Add("ImplicitUsings enable");
        }

        if (changes.Count > 0)
            await File.WriteAllTextAsync(project.CsprojPath, text, ct);
        return changes;
    }

    /// <summary>
    /// Ensure <c>_Imports.razor</c> carries the standard Blazor usings. Returns true when changed.
    /// </summary>
    public static async Task<bool> EnsureStandardImportsAsync(string projectDir, CancellationToken ct = default)
    {
        var changed = false;
        foreach (var ns in StandardUsings)
            changed |= await ImportsUpdater.EnsureUsingAsync(projectDir, ns, ct);
        return changed;
    }
}
