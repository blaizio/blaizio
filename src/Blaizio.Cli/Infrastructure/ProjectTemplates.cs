namespace Blaizio.Cli.Infrastructure;

/// <summary>The csproj texts <c>new</c> scaffolds and the Showcase template's component set.</summary>
internal static class ProjectTemplates
{
    /// <summary>The WASM project file scaffolded for the Showcase template.</summary>
    public static string ShowcaseCsproj(string projectName) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{projectName}</RootNamespace>
            <AssemblyName>{projectName}</AssemblyName>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.8" />
            <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.8" PrivateAssets="all" />
            <PackageReference Include="Blaizio.Base" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="Blaizio.Icons" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="TailwindMerge.NET" Version="{PackageVersions.TailwindMerge}" />
          </ItemGroup>

        </Project>

        """;

    /// <summary>The Razor class library project file scaffolded for the Library template.</summary>
    public static string LibraryCsproj(string projectName) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.Razor">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{projectName}</RootNamespace>
            <AssemblyName>{projectName}</AssemblyName>
          </PropertyGroup>

          <ItemGroup>
            <FrameworkReference Include="Microsoft.AspNetCore.App" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="Blaizio.Base" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="Blaizio.Icons" Version="{PackageVersions.Blaizio}" />
            <PackageReference Include="TailwindMerge.NET" Version="{PackageVersions.TailwindMerge}" />
          </ItemGroup>

        </Project>

        """;

    /// <summary>The component set the Showcase demo pages use.</summary>
    public static readonly string[] ShowcaseComponents =
    [
        // shell
        "button", "kbd", "sheet", "command", "dialog", "theme-switcher",
        // dashboard
        "badge", "card", "alert", "separator", "tabs", "table", "avatar", "progress", "skeleton",
        // forms + auth
        "field", "label", "input-text", "input-number", "input-date", "select", "combobox",
        "checkbox", "radio-group", "switch", "slider",
        // overlays
        "alert-dialog", "popover", "tooltip", "dropdown-menu", "toast",
        // data
        "accordion", "collapsible", "tree", "carousel",
    ];
}
