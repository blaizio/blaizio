namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// What kind of thing an item reference names. Plain names are the only ones that need the
/// project's default registry; everything else carries its own source, which is why a command can
/// skip the default registry's preflight when nothing asked for it.
/// </summary>
public static class ItemReference
{
    /// <summary>
    /// True when the reference resolves without the default registry: a namespace recorded in
    /// <c>blaizio.json</c>, a direct URL, a file on disk, or an <c>owner/repo/item</c> address.
    /// </summary>
    public static bool IsSelfSourced(string reference)
    {
        if (reference.Length == 0)
            return false;

        return reference[0] == '@'
            || reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(reference)
            || GitHubAddress.TryParse(reference, out _);
    }

    /// <summary>
    /// True when resolving <paramref name="references"/> has to reach the default registry: any
    /// plain name does, and so does an empty list (the interactive picker reads its catalogue).
    /// </summary>
    public static bool NeedsDefaultRegistry(IReadOnlyList<string> references) =>
        references.Count == 0 || references.Any(reference => !IsSelfSourced(reference));
}
