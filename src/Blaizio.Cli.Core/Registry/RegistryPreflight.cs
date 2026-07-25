namespace Blaizio.Cli.Core.Registry;

/// <summary>What a preflight probe found.</summary>
/// <param name="Reachable">False only when nothing answered - the run cannot succeed.</param>
/// <param name="Message">The failure text to show, or null when reachable.</param>
/// <param name="HasIndex">Whether an index came back (a reachable registry may ship none).</param>
public readonly record struct RegistryStatus(bool Reachable, string? Message, bool HasIndex)
{
    /// <summary>A reachable registry that answered with an index.</summary>
    public static RegistryStatus Ok { get; } = new(true, null, true);
}

/// <summary>
/// Answers "is this registry there?" before a command starts changing the project. Commands that
/// wire a project up and then fetch components (<c>add</c>, <c>update</c>) would otherwise install
/// packages, write the tokens file and edit the host page, and only then discover the registry is
/// unreachable - leaving a half-applied project behind a confusing late error.
/// <para>
/// Only a registry that answers nothing at all is fatal. A reachable registry with no
/// <c>index.json</c> is fine (v1 raw sources and third-party registries ship items without one),
/// so a missing index reports <see cref="RegistryStatus.HasIndex"/> false and lets the run proceed.
/// </para>
/// </summary>
public static class RegistryPreflight
{
    /// <summary>Probe <paramref name="registry"/>, classifying what came back.</summary>
    public static async Task<RegistryStatus> CheckAsync(
        IRegistryClient registry,
        CancellationToken ct = default)
    {
        try
        {
            await registry.GetIndexAsync(ct);
            return RegistryStatus.Ok;
        }
        catch (RegistryException ex) when (ex.Reason is RegistryFailure.NotFound)
        {
            // Answered, just no index: items still resolve at the base path.
            return new RegistryStatus(Reachable: true, Message: null, HasIndex: false);
        }
        catch (RegistryException ex)
        {
            return new RegistryStatus(Reachable: false, Message: ex.Message, HasIndex: false);
        }
    }
}
