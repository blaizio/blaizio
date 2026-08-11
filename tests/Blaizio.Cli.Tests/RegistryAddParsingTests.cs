using Blaizio.Cli.Commands;
using Xunit;

namespace Blaizio.Cli.Tests;

/// <summary>
/// What <c>registry add</c> accepts on the command line: the <c>@namespace=address</c> pair, and
/// the header/parameter options a private registry needs.
/// </summary>
public class RegistryAddParsingTests
{
    [Theory]
    [InlineData("@acme=https://acme.dev/r")]
    // Templated addresses: braces are not legal URI characters, so the check fills them first.
    [InlineData("@acme=https://acme.dev/api/{name}")]
    [InlineData("@acme=https://acme.dev/{style}/{name}.json")]
    public void Accepts_an_address(string entry)
        => Assert.True(RegistryAddCommand.TryParse(entry, out _, out _, out _));

    [Theory]
    // No namespace, no URL, and a {style} that cannot place an item on its own.
    [InlineData("acme=https://acme.dev/r")]
    [InlineData("@acme")]
    [InlineData("@acme=not a url")]
    [InlineData("@acme=https://acme.dev/{style}/tag.json")]
    public void Rejects_a_malformed_entry(string entry)
        => Assert.False(RegistryAddCommand.TryParse(entry, out _, out _, out _));

    [Fact]
    public void A_shell_eaten_namespace_is_named_as_such()
    {
        // PowerShell reads a bare @name as its splatting operator, so an unquoted @acme=url
        // arrives with the namespace expanded to nothing - a leading '=' is that fingerprint.
        Assert.False(RegistryAddCommand.TryParse("=https://acme.dev/r", out _, out _, out var problem));

        Assert.Contains("splatting", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quote", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("@default=https://acme.dev/r")]
    [InlineData("@Default=https://acme.dev/r")]
    public void The_default_namespace_cannot_be_recorded(string entry)
    {
        // @default already names the project's own registry, so mapping it elsewhere would
        // silently redirect every @default/item dependency away from the consumer's registry.
        Assert.False(RegistryAddCommand.TryParse(entry, out _, out _, out var problem));
        Assert.Contains("reserved", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("@blaizio=https://acme.dev/r")]
    [InlineData("@Blaizio=https://acme.dev/r")]
    public void A_namespace_that_shadows_the_package_root_is_rejected(string entry)
    {
        // Its installs would land in a Blaizio folder, and C# then binds every Blaizio.Base
        // reference inside them to that nested segment instead of the package.
        Assert.False(RegistryAddCommand.TryParse(entry, out _, out _, out var problem));
        Assert.Contains("Blaizio", problem, StringComparison.Ordinal);
    }

    [Theory]
    // Only the exact folder name shadows: C# name lookup is case sensitive, and a namespace that
    // merely starts with it nests under a distinct segment.
    [InlineData("@BLAIZIO=https://acme.dev/r")]
    [InlineData("@blaizio-ui=https://acme.dev/r")]
    public void A_namespace_that_only_resembles_the_package_root_is_accepted(string entry)
        => Assert.True(RegistryAddCommand.TryParse(entry, out _, out _, out _));

    [Fact]
    public void A_header_keeps_everything_after_the_first_colon()
    {
        Assert.True(RegistryAddCommand.TryParsePairs(
            ["Authorization: Bearer ${T}", "X-Scope: a:b:c"], ':', "--header", "example", out var headers, out _));

        Assert.Equal("Bearer ${T}", headers["Authorization"]);
        Assert.Equal("a:b:c", headers["X-Scope"]);
    }

    [Fact]
    public void A_param_splits_on_the_first_equals()
    {
        Assert.True(RegistryAddCommand.TryParsePairs(
            ["token=abc=def"], '=', "--param", "example", out var parameters, out _));

        Assert.Equal("abc=def", parameters["token"]);
    }

    [Theory]
    [InlineData("Authorization Bearer x")]
    [InlineData(": value")]
    public void A_pair_without_a_name_is_rejected(string entry)
    {
        Assert.False(RegistryAddCommand.TryParsePairs([entry], ':', "--header", "example", out _, out var problem));
        Assert.NotEmpty(problem);
    }
}
