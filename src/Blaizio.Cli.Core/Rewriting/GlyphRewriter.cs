using System.Text.RegularExpressions;
using Blaizio.Cli.Core.Styling;

namespace Blaizio.Cli.Core.Rewriting;

/// <summary>
/// Retargets the <c>BzGlyphs</c> file - the one place the styled components name an icon set -
/// at another set. Each <c>public static Icon Name => Some.Set.Icon;</c> member whose name the
/// <see cref="GlyphCatalog"/> knows gets that set's member; a member the table does not know (one
/// the user added) is left exactly as written. Applied on the way in by the component writer
/// (so an install or update lands the file already pointed at the recorded set, with a ledger
/// hash to match) and in place by <c>apply --icons</c>.
/// </summary>
public sealed partial class GlyphRewriter
{
    /// <summary>The file the rewriter applies to, by name.</summary>
    public const string FileName = "BzGlyphs.cs";

    private readonly string _set;

    /// <param name="set">An <see cref="IconSetCatalog"/> name.</param>
    public GlyphRewriter(string set)
    {
        _set = set.ToLowerInvariant();
    }

    /// <summary>The set this rewriter targets.</summary>
    public string Set => _set;

    /// <summary>
    /// A rewriter for <paramref name="set"/>, or null when the set is the default (the file
    /// already ships pointed at it) or not a set the catalog knows.
    /// </summary>
    public static GlyphRewriter? For(string? set) =>
        set is null || string.Equals(set, IconSetCatalog.Default, StringComparison.OrdinalIgnoreCase)
            || IconSetCatalog.Find(set) is null
            ? null
            : new GlyphRewriter(set);

    /// <summary>Whether <paramref name="path"/> (any separator) is the glyph file.</summary>
    public static bool IsGlyphFile(string path) =>
        path.EndsWith(FileName, StringComparison.Ordinal)
        && (path.Length == FileName.Length || path[^(FileName.Length + 1)] is '/' or '\\');

    /// <summary>Return <paramref name="content"/> with every known member pointed at this set.</summary>
    public string Rewrite(string content) =>
        Member().Replace(content, m =>
            GlyphCatalog.Member(_set, m.Groups["name"].Value) is { } member
                ? $"{m.Groups["lead"].Value}{member};"
                : m.Value);

    // "public static Icon Check => Tabler.Outline.Check;" - the lead keeps the declaration and
    // the arrow so only the expression changes; the expression is any dotted identifier.
    [GeneratedRegex(@"(?<lead>public\s+static\s+Icon\s+(?<name>\w+)\s*=>\s*)[\w.]+\s*;")]
    private static partial Regex Member();
}
