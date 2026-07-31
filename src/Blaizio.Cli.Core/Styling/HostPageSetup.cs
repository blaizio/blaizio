using System.Text.RegularExpressions;

namespace Blaizio.Cli.Core.Styling;

/// <summary>The outcome of wiring the host page, for reporting.</summary>
public sealed class HostPageResult
{
    /// <summary>The patched host page (project-relative, POSIX), or null when no host was found (e.g. a class library).</summary>
    public string? HostPath { get; init; }

    /// <summary>Human-readable changes applied this run. Empty when everything was already wired.</summary>
    public IReadOnlyList<string> Changes { get; init; } = [];
}

/// <summary>
/// Wires Blaizio into the app's HTML host page - whichever flavour the project has: a WASM
/// <c>wwwroot/index.html</c>, a Blazor Web App <c>Components/App.razor</c>, or a Blazor Server
/// <c>Pages/_Host.cshtml</c> / <c>_Layout.cshtml</c>. Two idempotent patches: the compiled
/// stylesheet <c>&lt;link&gt;</c> and the pre-paint <c>boot.js</c> <c>&lt;script&gt;</c> in
/// <c>&lt;head&gt;</c>. The v3 look lives inlined in the components and the tokens file, so no
/// <c>style-*</c>/<c>preset-*</c> class exists anymore — a stale one left by a v1 init is
/// stripped; <c>.dark</c> stays the only class that matters on <c>&lt;html&gt;</c> and is the
/// app's (boot.js re-applies the user's persisted choice before first paint). The <c>dir</c>
/// attribute is never touched: the config's <c>rtl</c> flag means "RTL support", not "this page
/// is RTL" - page direction is the app's to set.
/// </summary>
public sealed partial class HostPageSetup
{
    /// <summary>The boot script every host needs (re-applies persisted theme/style/dir before first paint).</summary>
    public const string BootScript = "_content/blaizio.base/dist/boot.js";

    /// <summary>The host pages a patch may touch (project-relative), for callers that snapshot
    /// before mutating - e.g. the add transaction's rollback.</summary>
    public static IReadOnlyList<string> CandidatePages => s_candidates;

    // Candidate host pages, checked in order. A candidate only qualifies if it actually contains a
    // </head> - a WASM root App.razor (the Router) or a Server routes-only App.razor never matches.
    private static readonly string[] s_candidates =
    [
        "wwwroot/index.html",   // Blazor WebAssembly standalone
        "Components/App.razor", // Blazor Web App (.NET 8+): Server, WASM or Auto render modes
        "App.razor",            // Web App variants rooting the shell at the project root
        "Pages/_Host.cshtml",   // Blazor Server (.NET 7)
        "Pages/_Layout.cshtml", // Blazor Server (.NET 6)
    ];

    /// <summary>
    /// Ensure the host page carries the Blaizio wiring. <paramref name="cssHref"/> is the compiled
    /// stylesheet's href (the Tailwind output path relative to <c>wwwroot</c>). Any
    /// <c>style-*</c>/<c>preset-*</c> class a v1 init left on <c>&lt;html&gt;</c> is stripped —
    /// they're dead in v3 (the look ships inlined in the components).
    /// </summary>
    public async Task<HostPageResult> EnsureAsync(
        string projectDir, string cssHref = "app.css", CancellationToken ct = default)
    {
        var host = FindHost(projectDir, out var content);
        if (host is null || content is null)
            return new HostPageResult();

        var changes = new List<string>();

        content = RemoveHtmlClasses(content, changes);
        content = EnsureHeadLine(content, $"href=\"{cssHref}\"", $"<link rel=\"stylesheet\" href=\"{cssHref}\" />",
            $"stylesheet link ({cssHref})", changes);
        content = EnsureHeadLine(content, BootScript, $"<script src=\"{BootScript}\"></script>",
            "boot.js script", changes);

        if (changes.Count > 0)
            await File.WriteAllTextAsync(Path.Combine(projectDir, host), content, ct);

        return new HostPageResult { HostPath = host, Changes = changes };
    }

    /// <summary>The attribute marking the CLI-written webfont link, so ensure/remove never touch
    /// a font link the app added itself.</summary>
    public const string FontLinkMarker = "data-blaizio=\"fonts\"";

    /// <summary>
    /// Sync the CLI-managed Google Fonts stylesheet link in the host page's <c>&lt;head&gt;</c>.
    /// The webfonts a /create selection carries must actually load from somewhere: a CSS
    /// <c>@import</c> inside the managed overlay would end up inlined mid-bundle by Tailwind
    /// (where imports are ignored), so the host page carries it as a plain <c>&lt;link&gt;</c>
    /// instead. <paramref name="cssUrl"/> null removes the managed link (a selection with no
    /// webfonts); a differing URL swaps it. Idempotent, and only ever touches the line marked
    /// <see cref="FontLinkMarker"/>.
    /// </summary>
    public async Task<HostPageResult> EnsureFontLinkAsync(
        string projectDir, string? cssUrl, CancellationToken ct = default)
    {
        var host = FindHost(projectDir, out var content);
        if (host is null || content is null)
            return new HostPageResult();

        var changes = new List<string>();
        var line = cssUrl is null ? null : $"<link rel=\"stylesheet\" href=\"{cssUrl}\" {FontLinkMarker} />";

        // Already carrying exactly this link: nothing to do.
        if (line is null || !content.Contains(line, StringComparison.Ordinal))
        {
            content = RemoveHeadLine(content, FontLinkMarker, "webfonts link", changes);
            if (line is not null)
                content = EnsureHeadLine(content, FontLinkMarker, line, "webfonts link", changes);
        }

        if (changes.Count > 0)
            await File.WriteAllTextAsync(Path.Combine(projectDir, host), content, ct);

        return new HostPageResult { HostPath = host, Changes = changes };
    }

    /// <summary>
    /// Reverse of <see cref="EnsureAsync"/> for <c>uninstall</c>: strip the Blaizio wiring from the
    /// host page — the <c>boot.js</c> script line, the stylesheet link for
    /// <paramref name="cssHref"/>, and the <c>style-*</c>/<c>preset-*</c> classes on
    /// <c>&lt;html&gt;</c>. Everything else in the page is the app's and stays untouched.
    /// </summary>
    public async Task<HostPageResult> RemoveAsync(
        string projectDir, string cssHref = "app.css", bool dryRun = false, CancellationToken ct = default)
    {
        var host = FindHost(projectDir, out var content);
        if (host is null || content is null)
            return new HostPageResult();

        var changes = new List<string>();

        content = RemoveHeadLine(content, BootScript, "boot.js script", changes);
        content = RemoveHeadLine(content, $"href=\"{cssHref}\"", $"stylesheet link ({cssHref})", changes,
            requireMarker: "stylesheet");
        content = RemoveHeadLine(content, FontLinkMarker, "webfonts link", changes);
        content = RemoveHtmlClasses(content, changes);

        if (changes.Count > 0 && !dryRun)
            await File.WriteAllTextAsync(Path.Combine(projectDir, host), content, ct);

        return new HostPageResult { HostPath = host, Changes = changes };
    }

    /// <summary>
    /// True when the project's host page already carries the Blaizio wiring (it loads
    /// <see cref="BootScript"/>). Once wired, the page is the app's to evolve - <c>update</c> uses
    /// this to skip host patching entirely instead of re-guessing hrefs or classes.
    /// </summary>
    public bool IsWired(string projectDir)
        => FindHost(projectDir, out var content) is not null
           && content!.Contains(BootScript, StringComparison.OrdinalIgnoreCase);

    /// <summary>The host page's content for read-only inspection (font detection); null when no host.</summary>
    public static string? FindHostContent(string projectDir) =>
        FindHost(projectDir, out var content) is null ? null : content;

    private static string? FindHost(string projectDir, out string? content)
    {
        foreach (var candidate in s_candidates)
        {
            var abs = Path.Combine(projectDir, candidate);
            if (!File.Exists(abs))
                continue;
            var text = File.ReadAllText(abs);
            if (text.Contains("</head>", StringComparison.OrdinalIgnoreCase))
            {
                content = text;
                return candidate;
            }
        }

        content = null;
        return null;
    }

    // Remove the whole line that carries the marker (the reverse of EnsureHeadLine). With
    // requireMarker set, the line must also contain that second token — so a stylesheet link is
    // only removed when the matching href sits on an actual <link rel="stylesheet"> line.
    private static string RemoveHeadLine(
        string content, string marker, string label, List<string> changes, string? requireMarker = null)
    {
        var index = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return content;

        var lineStart = content.LastIndexOf('\n', index) + 1;
        var lineEnd = content.IndexOf('\n', index);
        lineEnd = lineEnd < 0 ? content.Length : lineEnd + 1;

        var line = content[lineStart..lineEnd];
        if (requireMarker is not null && !line.Contains(requireMarker, StringComparison.OrdinalIgnoreCase))
            return content;

        changes.Add($"{label} removed");
        return content.Remove(lineStart, lineEnd - lineStart);
    }

    // Strip the style-* and preset-* classes from <html> (dropping the class attribute when that
    // leaves it empty). The reverse of EnsureHtmlAttributes.
    private static string RemoveHtmlClasses(string content, List<string> changes)
    {
        var htmlTag = HtmlTagRegex().Match(content);
        if (!htmlTag.Success)
            return content;

        var tag = htmlTag.Value;
        var classAttr = ClassAttrRegex().Match(tag);
        if (!classAttr.Success)
            return content;

        var parts = classAttr.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = parts.Where(c =>
            !c.StartsWith("style-", StringComparison.Ordinal)
            && !c.StartsWith("preset-", StringComparison.Ordinal)).ToArray();
        if (kept.Length == parts.Length)
            return content;

        foreach (var removed in parts.Except(kept))
            changes.Add($"class {removed} removed");

        var updated = kept.Length == 0
            ? tag.Replace(classAttr.Value, string.Empty).Replace("  ", " ").Replace(" >", ">")
            : tag.Replace(classAttr.Value, $"class=\"{string.Join(' ', kept)}\"");
        return content.Replace(tag, updated);
    }

    // Insert a line just above </head> (matching its indentation, one level deeper) when the
    // presence marker isn't anywhere in the file.
    private static string EnsureHeadLine(string content, string marker, string line, string label, List<string> changes)
    {
        if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            return content;

        var headClose = HeadCloseRegex().Match(content);
        if (!headClose.Success)
            return content;

        var indent = headClose.Groups[1].Value;
        changes.Add(label);
        return content.Insert(headClose.Index, $"{indent}    {line}\n");
    }

    [GeneratedRegex(@"<html\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"class\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttrRegex();

    [GeneratedRegex(@"([ \t]*)</head>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadCloseRegex();
}
