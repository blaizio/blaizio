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
/// <c>Pages/_Host.cshtml</c> / <c>_Layout.cshtml</c>. Three idempotent patches: the
/// <c>style-&lt;skin&gt;</c> class (and <c>dir="rtl"</c> when RTL) on <c>&lt;html&gt;</c>, the compiled
/// stylesheet <c>&lt;link&gt;</c>, and the pre-paint <c>boot.js</c> <c>&lt;script&gt;</c> in
/// <c>&lt;head&gt;</c>. Re-running changes nothing that is already wired; a differing
/// <c>style-*</c> class is swapped to the configured skin (blaizio.json is the source of truth).
/// </summary>
public sealed partial class HostPageSetup
{
    /// <summary>The boot script every host needs (re-applies persisted theme/style/dir before first paint).</summary>
    public const string BootScript = "_content/blaizio.base/dist/boot.js";

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
    /// stylesheet's href (the Tailwind output path relative to <c>wwwroot</c>).
    /// </summary>
    public async Task<HostPageResult> EnsureAsync(
        string projectDir, string skin, bool rtl, string cssHref = "app.css", CancellationToken ct = default)
    {
        var host = FindHost(projectDir, out var content);
        if (host is null || content is null)
            return new HostPageResult();

        var changes = new List<string>();

        content = EnsureHtmlAttributes(content, skin, rtl, changes);
        content = EnsureHeadLine(content, $"href=\"{cssHref}\"", $"<link rel=\"stylesheet\" href=\"{cssHref}\" />",
            $"stylesheet link ({cssHref})", changes);
        content = EnsureHeadLine(content, BootScript, $"<script src=\"{BootScript}\"></script>",
            "boot.js script", changes);

        if (changes.Count > 0)
            await File.WriteAllTextAsync(Path.Combine(projectDir, host), content, ct);

        return new HostPageResult { HostPath = host, Changes = changes };
    }

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

    // Patch the <html> tag: ensure a style-<skin> class (swapping a differing style-*), and add
    // dir="rtl" when RTL is configured and no dir is set (an existing dir is the app's to own).
    private static string EnsureHtmlAttributes(string content, string skin, bool rtl, List<string> changes)
    {
        var htmlTag = HtmlTagRegex().Match(content);
        if (!htmlTag.Success)
            return content;

        var tag = htmlTag.Value;
        var updated = tag;

        var skinClass = $"style-{skin}";
        var classAttr = ClassAttrRegex().Match(updated);
        if (classAttr.Success)
        {
            var classes = classAttr.Groups[1].Value;
            if (!classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(skinClass))
            {
                var existingSkin = StyleClassRegex().Match(classes);
                var replaced = existingSkin.Success
                    ? classes.Replace(existingSkin.Value, skinClass)
                    : string.IsNullOrWhiteSpace(classes) ? skinClass : $"{classes} {skinClass}";
                updated = updated.Replace(classAttr.Value, $"class=\"{replaced}\"");
                changes.Add(existingSkin.Success
                    ? $"skin class {existingSkin.Value} -> {skinClass}"
                    : $"skin class {skinClass}");
            }
        }
        else
        {
            updated = updated.Insert(updated.Length - 1, $" class=\"{skinClass}\"");
            changes.Add($"skin class {skinClass}");
        }

        if (rtl && !updated.Contains("dir=", StringComparison.OrdinalIgnoreCase))
        {
            updated = updated.Insert(updated.Length - 1, " dir=\"rtl\"");
            changes.Add("dir=\"rtl\"");
        }

        return updated == tag ? content : content.Replace(tag, updated);
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

    [GeneratedRegex(@"style-[A-Za-z0-9-]+")]
    private static partial Regex StyleClassRegex();

    [GeneratedRegex(@"([ \t]*)</head>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadCloseRegex();
}
