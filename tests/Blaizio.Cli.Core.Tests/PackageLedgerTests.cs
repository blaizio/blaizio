using Blaizio.Cli.Core.Configuration;
using Blaizio.Cli.Core.Dotnet;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The package ledger is uninstall's undo record: only ids the CLI itself introduced are recorded;
/// pre-existing references are user-owned and never enter it.
/// </summary>
public class PackageLedgerTests
{
    [Fact]
    public void PreExisting_finds_ids_already_referenced_in_the_csproj()
    {
        using var dir = new TempDir();
        var csproj = dir.Combine("App.csproj");
        File.WriteAllText(csproj,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Blaizio.Base" Version="0.1.0" />
              </ItemGroup>
            </Project>
            """);

        var pre = PackageLedger.PreExisting(csproj, ["Blaizio.Base", "Blaizio.Icons"]);

        Assert.Contains("Blaizio.Base", pre);
        Assert.DoesNotContain("Blaizio.Icons", pre);
    }

    [Fact]
    public void PreExisting_is_empty_without_a_csproj() =>
        Assert.Empty(PackageLedger.PreExisting(null, ["Blaizio.Base"]));

    [Fact]
    public void Record_skips_preexisting_and_duplicate_ids()
    {
        var config = new BlaizioConfig { Namespace = "App.Components.Ui" };
        var pre = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Blaizio.Base" };

        var changed = PackageLedger.Record(config, ["Blaizio.Base", "Blaizio.Icons", "TailwindMerge.NET"], pre);

        Assert.True(changed);
        Assert.Equal(["Blaizio.Icons", "TailwindMerge.NET"], config.Packages);

        // A second run (same ids, different casing) changes nothing.
        Assert.False(PackageLedger.Record(config, ["blaizio.icons"], pre));
        Assert.Equal(2, config.Packages.Count);
    }
}
