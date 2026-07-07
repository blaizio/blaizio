using Blaizio.Cli.Core.Writing;

namespace Blaizio.Cli.Core.Operations;

/// <summary>The full outcome of an <c>add</c>, structured for both console reporting and <c>--json</c>.</summary>
public sealed class AddResult
{
    /// <summary>Item names installed, in dependency order.</summary>
    public required IReadOnlyList<string> Items { get; init; }

    /// <summary>NuGet packages that were (or would be) installed.</summary>
    public required IReadOnlyList<string> NugetPackages { get; init; }

    /// <summary>Every file touched, with its per-file action.</summary>
    public required IReadOnlyList<WrittenFile> Files { get; init; }

    /// <summary>The component namespace used.</summary>
    public required string Namespace { get; init; }

    /// <summary>Whether <c>_Imports.razor</c> gained a new <c>@using</c>.</summary>
    public required bool ImportsUpdated { get; init; }

    /// <summary>True when this was a dry run (nothing written).</summary>
    public required bool DryRun { get; init; }
}
