namespace Blaizio.Cli.Core.Registry;

/// <summary>
/// A repository address: <c>owner/repo/item</c>, optionally pinned with <c>#ref</c> (a branch, a
/// tag or a commit). The repository itself is the registry - its <c>registry.json</c> and the
/// source files beside it - so publishing needs no host at all.
/// </summary>
/// <param name="Owner">The GitHub owner.</param>
/// <param name="Repo">The repository.</param>
/// <param name="Item">The registry item name (never a file path).</param>
/// <param name="Ref">The branch, tag or commit, or null for the default branch.</param>
public sealed record GitHubAddress(string Owner, string Repo, string Item, string? Ref)
{
    /// <summary>The repository this address points into, for trust decisions and messages.</summary>
    public string Repository => $"{Owner}/{Repo}";

    /// <summary>The ref to fetch at: an explicit one, else the repository's default branch.</summary>
    public string Reference => Ref ?? "HEAD";

    /// <summary>Where the raw files live, with a trailing slash.</summary>
    public string RawRoot => $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Reference}/";

    /// <summary>The address as it was written, for messages and install records.</summary>
    public override string ToString() => Ref is null ? $"{Owner}/{Repo}/{Item}" : $"{Owner}/{Repo}/{Item}#{Ref}";

    /// <summary>
    /// Parse <c>owner/repo/item</c> (with an optional <c>#ref</c>). Rejects anything that is
    /// already something else: a URL, a namespace, a Windows or POSIX path, or an address ending
    /// in <c>.json</c> - those are file references, and the caller handles them as such.
    /// </summary>
    public static bool TryParse(string reference, out GitHubAddress address)
    {
        address = null!;
        if (reference.Length == 0
            || reference[0] is '@' or '.' or '/' or '\\' or '~'
            || reference.Contains("://", StringComparison.Ordinal)
            || reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(reference))
        {
            return false;
        }

        var address_ = reference;
        string? gitRef = null;
        var hash = reference.IndexOf('#');
        if (hash >= 0)
        {
            gitRef = reference[(hash + 1)..].Trim();
            address_ = reference[..hash];
            if (gitRef.Length == 0)
                return false;
        }

        // owner / repo / item - and the item name may not itself be a path.
        var parts = address_.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts.Any(p => p.Length == 0))
            return false;
        if (parts.Any(p => p is "." or ".."))
            return false;

        address = new GitHubAddress(parts[0], parts[1], parts[2], gitRef);
        return true;
    }
}
