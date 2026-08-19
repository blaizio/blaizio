using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Operations;
using Blaizio.Cli.Core.Registry;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class PackageVersionTests
{
    [Theory]
    // Release ordering.
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("1.2.3", "1.2.3", 0)]
    [InlineData("1.10.0", "1.9.0", 1)]
    // A release outranks its own prereleases.
    [InlineData("0.1.0-alpha.24", "0.1.0", -1)]
    [InlineData("0.1.0", "0.1.0-alpha.24", 1)]
    // Prerelease identifiers: numerics numeric, and the dogfood revision shape.
    [InlineData("0.1.0-alpha.23", "0.1.0-alpha.24", -1)]
    [InlineData("0.1.0-alpha.23.19", "0.1.0-alpha.24", -1)]
    [InlineData("0.1.0-alpha.24.3", "0.1.0-alpha.24", 1)]
    [InlineData("0.1.0-alpha.24", "0.1.0-alpha.24", 0)]
    // Numeric identifiers rank below alphanumeric ones (SemVer 11.4.3).
    [InlineData("1.0.0-1", "1.0.0-alpha", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    public void Orders_versions_by_semver_precedence(string left, string right, int expected)
    {
        Assert.True(PackageVersion.TryCompare(left, right, out var result));
        Assert.Equal(expected, Math.Sign(result));
    }

    [Theory]
    [InlineData("0.1.0-alpha.*")]  // floating restore - resolves forward, nothing to order
    [InlineData("[1.0.0,2.0.0)")]  // range
    [InlineData("1.0")]            // not three-part
    [InlineData("")]
    [InlineData(null)]
    public void Refuses_to_compare_what_is_not_a_plain_version(string? version)
    {
        Assert.False(PackageVersion.TryCompare(version, "1.0.0", out _));
        Assert.False(PackageVersion.TryCompare("1.0.0", version, out _));
    }
}

public class BaseVersionGuardTests
{
    private static RegistryItem Item(string name, string? minBase) => new()
    {
        Name = name,
        MinBase = minBase,
    };

    [Fact]
    public void Fails_when_the_pinned_base_predates_an_items_minimum()
    {
        var message = BaseVersionGuard.Check([Item("panel", "0.1.0-alpha.24")], "0.1.0-alpha.23");

        Assert.NotNull(message);
        Assert.Contains("panel", message);
        Assert.Contains("0.1.0-alpha.24", message);
        Assert.Contains("0.1.0-alpha.23", message);
        Assert.Contains("blaizio update", message);
    }

    [Fact]
    public void The_strictest_item_in_the_graph_is_the_one_named()
    {
        var message = BaseVersionGuard.Check(
            [Item("button", "0.1.0-alpha.20"), Item("panel", "0.1.0-alpha.24")], "0.1.0-alpha.22");

        Assert.NotNull(message);
        Assert.Contains("panel", message);
    }

    [Theory]
    [InlineData("0.1.0-alpha.24")]  // exactly the minimum
    [InlineData("0.1.0-alpha.25")]  // newer prerelease
    [InlineData("0.1.0")]           // the release itself
    public void Passes_when_the_pin_satisfies_the_minimum(string referenced)
    {
        Assert.Null(BaseVersionGuard.Check([Item("panel", "0.1.0-alpha.24")], referenced));
    }

    [Theory]
    [InlineData(null)]             // no Blaizio.Base reference: fresh install pulls current
    [InlineData("0.1.0-alpha.*")]  // floating: resolves forward on restore
    public void Passes_when_there_is_no_definite_pin_to_judge(string? referenced)
    {
        Assert.Null(BaseVersionGuard.Check([Item("panel", "0.1.0-alpha.24")], referenced));
    }

    [Fact]
    public void Passes_when_no_item_declares_a_minimum()
    {
        Assert.Null(BaseVersionGuard.Check([Item("button", null)], "0.1.0-alpha.1"));
    }
}
