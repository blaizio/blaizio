using Blaizio.Cli.Core.Dotnet;
using Blaizio.Cli.Core.Registry;

namespace Blaizio.Cli.Core.Resolution;

/// <summary>
/// The flattened result of resolving one or more requested items: every item to install in
/// dependency order (dependencies first), plus the union of NuGet packages they need.
/// </summary>
public sealed class ResolvedGraph
{
    /// <summary>Items to install, dependencies before dependents, each appearing once.</summary>
    public required IReadOnlyList<RegistryItem> Items { get; init; }

    /// <summary>Distinct NuGet packages across the whole graph, pins already reconciled.</summary>
    public required IReadOnlyList<NugetDependency> NugetPackages { get; init; }

    /// <summary>
    /// Distinct development-only NuGet packages (installed then marked <c>PrivateAssets="all"</c>).
    /// Never overlaps <see cref="NugetPackages"/> - a package some item needs at runtime is a
    /// runtime package, whatever another item says.
    /// </summary>
    public IReadOnlyList<NugetDependency> DevNugetPackages { get; init; } = [];

    /// <summary>Names the caller asked for directly (a subset of <see cref="Items"/>).</summary>
    public required IReadOnlyList<string> Requested { get; init; }
}
