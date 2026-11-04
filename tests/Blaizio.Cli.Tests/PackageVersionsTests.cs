using Blaizio.Cli.Infrastructure;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>What an update pins: the base set always, an optional package only when it is referenced.</summary>
public class PackageVersionsTests
{
    [Fact]
    public void An_update_moves_the_syntax_packages_a_project_references_and_never_introduces_them()
    {
        var none = PackageVersions.ForUpdate(new HashSet<string>());
        var referenced = PackageVersions.ForUpdate(new HashSet<string> { "Blaizio.Syntax", "Blaizio.Syntax.Languages", "Blaizio.Icons.Lucide" });

        Assert.Equal(PackageVersions.BaseSet.Select(p => p.Id), none.Select(p => p.Id));
        Assert.Equal(
            [.. PackageVersions.BaseSet.Select(p => p.Id), "Blaizio.Icons.Lucide", "Blaizio.Syntax", "Blaizio.Syntax.Languages"],
            referenced.Select(p => p.Id));
        Assert.All(referenced.Where(p => p.Id.StartsWith("Blaizio.", StringComparison.Ordinal)), p => Assert.Equal(PackageVersions.Blaizio, p.Version));
    }

    [Fact]
    public void Every_versioned_package_is_listed_once()
    {
        var ids = PackageVersions.All.Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("Blaizio.Syntax", ids);
        Assert.Contains("Blaizio.Syntax.Languages", ids);
    }
}
