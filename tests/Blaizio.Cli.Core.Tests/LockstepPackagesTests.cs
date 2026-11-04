using Blaizio.Cli.Core.Dotnet;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The version an install gives Blaizio's own packages: the project's Blaizio.Base version for one
/// it introduces, so <c>add code-block</c> never brings a Blaizio.Syntax newer than its Base.
/// </summary>
public class LockstepPackagesTests
{
    private static Dictionary<string, string?> References(params (string Id, string? Version)[] references) =>
        references.ToDictionary(r => r.Id, r => r.Version, StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("Blaizio.Base", true)]
    [InlineData("Blaizio.Icons", true)]
    [InlineData("Blaizio.Icons.Lucide", true)]
    [InlineData("blaizio.syntax", true)]
    [InlineData("Blaizio.Syntax.Languages", true)]
    [InlineData("Blaizio.Cli", false)]
    [InlineData("Blaizio.SyntaxFoo", false)]
    [InlineData("TailwindMerge.NET", false)]
    public void Knows_which_packages_move_together(string id, bool lockstep) =>
        Assert.Equal(lockstep, LockstepPackages.Contains(id));

    [Fact]
    public void A_new_lockstep_package_takes_the_projects_base_version()
    {
        var pinned = LockstepPackages.PinTo(
            [new("Blaizio.Base"), new("Blaizio.Syntax"), new("Blaizio.Icons.Phosphor"), new("TailwindMerge.NET"), new("Net.Codecrete.QrCodeGenerator")],
            References(("Blaizio.Base", "0.3.6"), ("TailwindMerge.NET", "1.4.0")));

        Assert.Equal(
            ["Blaizio.Base", "Blaizio.Syntax@0.3.6", "Blaizio.Icons.Phosphor@0.3.6", "TailwindMerge.NET", "Net.Codecrete.QrCodeGenerator"],
            pinned.Select(p => p.ToString()));
    }

    [Fact]
    public void A_referenced_package_and_an_explicit_pin_are_left_alone()
    {
        var pinned = LockstepPackages.PinTo(
            [new("Blaizio.Icons"), new("Blaizio.Syntax", "0.3.7")],
            References(("Blaizio.Base", "0.3.6"), ("Blaizio.Icons", "0.3.5")));

        Assert.Equal(["Blaizio.Icons", "Blaizio.Syntax@0.3.7"], pinned.Select(p => p.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0.3.*")]
    [InlineData("[0.3.6,)")]
    [InlineData("0.3.6.12")]
    public void Without_a_plain_base_version_nothing_is_pinned(string? baseVersion)
    {
        var references = baseVersion is null ? References() : References(("Blaizio.Base", baseVersion));

        var pinned = LockstepPackages.PinTo([new("Blaizio.Syntax")], references);

        Assert.Equal(["Blaizio.Syntax"], pinned.Select(p => p.ToString()));
    }
}
