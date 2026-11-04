using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Infrastructure;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// Blaizio's packages are listed in places nothing compiles together: the projects that pack under
/// <c>BlaizioVersionBase</c>, the publish workflow's pack loop, the docs site's floating rows in
/// Directory.Packages.props, and this tool's own tables (<see cref="PackageVersions"/>,
/// <see cref="LockstepPackages"/>). A package one of them misses ships unpublished, never moved by
/// <c>update</c>, or split from the rest, and only a release finds out. These hold every list to the
/// projects themselves.
/// </summary>
public class PackageFamilyTests
{
    [Fact]
    public void The_tool_versions_every_lockstep_package_and_nothing_else()
    {
        var lockstep = LockstepProjects();

        Assert.Equal(lockstep, PackageVersions.All.Select(p => p.Id).Where(IsBlaizio).Order(StringComparer.Ordinal));
        Assert.All(PackageVersions.All.Where(p => IsBlaizio(p.Id)), p => Assert.Equal(PackageVersions.Blaizio, p.Version));
        Assert.All(lockstep, id => Assert.True(LockstepPackages.Contains(id), $"{id} is not a lockstep package"));
    }

    [Fact]
    public void The_publish_workflow_packs_every_packable_project()
    {
        var loop = Regex.Match(Read(".github", "workflows", "publish.yml"), @"for project in ([^;]+);");
        Assert.True(loop.Success, "publish.yml has no 'for project in ...;' pack loop");

        Assert.Equal(
            Projects().Where(p => p.Packable).Select(p => p.Name).Order(StringComparer.Ordinal),
            loop.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_docs_rows_float_on_the_version_base_for_every_lockstep_package()
    {
        var versionBase = Regex.Match(Read("Directory.Build.props"), "<BlaizioVersionBase>(.+?)</BlaizioVersionBase>").Groups[1].Value;
        var rows = Regex.Matches(Read("Directory.Packages.props"), "<PackageVersion Include=\"(Blaizio\\.[^\"]+)\" Version=\"([^\"]+)\"")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);

        Assert.Equal(LockstepProjects(), rows.Keys.Order(StringComparer.Ordinal));
        Assert.All(rows, row => Assert.Equal($"{versionBase}.*", row.Value));
    }

    // Not a Blaizio package, but the same split: a bump of the row alone tests one merger and
    // installs another, and the components only ever run on the one the tool installs.
    [Fact]
    public void The_tool_installs_the_merger_the_repository_tests_against()
    {
        var row = Regex.Match(Read("Directory.Packages.props"), "<PackageVersion Include=\"TailwindMerge\\.NET\" Version=\"([^\"]+)\"");

        Assert.True(row.Success, "Directory.Packages.props has no TailwindMerge.NET row");
        Assert.Equal(row.Groups[1].Value, PackageVersions.TailwindMerge);
    }

    // A lockstep project packs as BlaizioPackageVersion, the version Directory.Build.props derives
    // from BlaizioVersionBase; the CLI packages carry a literal Version of their own.
    private static List<string> LockstepProjects() =>
        [.. Projects().Where(p => p.Lockstep).Select(p => p.Name).Order(StringComparer.Ordinal)];

    private static IEnumerable<(string Name, bool Packable, bool Lockstep)> Projects() =>
        Directory.GetFiles(Path.Combine(Root(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Text: File.ReadAllText(path)))
            .Select(p => (
                p.Name,
                p.Text.Contains("<IsPackable>true</IsPackable>", StringComparison.Ordinal)
                    || p.Text.Contains("<PackAsTool>true</PackAsTool>", StringComparison.Ordinal),
                p.Text.Contains("<Version>$(BlaizioPackageVersion)</Version>", StringComparison.Ordinal)));

    private static bool IsBlaizio(string id) => id.StartsWith("Blaizio.", StringComparison.Ordinal);

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([Root(), .. path]));

    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }
}
