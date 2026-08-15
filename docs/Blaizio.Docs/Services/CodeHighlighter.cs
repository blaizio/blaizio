using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace Blaizio.Docs.Services;

/// <summary>Turns a demo snippet into colored, line-numbered HTML.</summary>
/// <remarks>
/// Inputs are the docs' own embedded snippets - trusted, short, and finite. The contract is not a
/// general-purpose highlighter for arbitrary user strings.
/// </remarks>
public interface ICodeHighlighter
{
    /// <summary>
    /// Highlighted markup: token spans (<c>tok-*</c>, colored by the <c>--code*</c> palette) inside
    /// one <c>span.line</c> per line (numbered by a CSS counter - see Styles/app.css).
    /// <paramref name="language"/> pins the tokenizer (<c>yaml</c>, <c>text</c>) for shapes the
    /// content sniff cannot tell apart; null keeps the automatic detection.
    /// </summary>
    MarkupString Highlight(string code, string? language = null);
}

/// <summary>
/// A tiny Razor tokenizer replacing the highlight.js stack. The docs' snippets are short and
/// known-shape, so a single pass in C# produces the colors client-side JS used to - generated once
/// per distinct snippet (cached), shipped with zero script payload.
/// </summary>
internal sealed class CodeHighlighter : ICodeHighlighter
{
    // The embedded snippet set bounds practical growth, but the singleton lives for the process
    // and the interface accepts any string - the cap is the safety valve, not a tuning knob. At
    // capacity, new snippets render uncached rather than evicting (the docs never get there).
    private const int CacheCapacity = 512;

    private readonly ConcurrentDictionary<string, string> _cache = new();

    public MarkupString Highlight(string code, string? language = null)
    {
        // The NUL separator cannot appear in a language name, so a pinned "yaml" + snippet
        // never collides with an unpinned snippet that happens to open with "yaml". Written
        // as an ESCAPE - a literal NUL byte here made git treat this whole file as binary.
        var key = (language is null ? "" : language + "\u0000") + code.ReplaceLineEndings("\n");
        if (_cache.TryGetValue(key, out var cached))
            return new(cached);

        var rendered = Render(code.ReplaceLineEndings("\n"), language);
        if (_cache.Count < CacheCapacity)
            _cache.TryAdd(key, rendered);
        return new(rendered);
    }

    private static readonly HashSet<string> Keywords =
    [
        "private", "public", "protected", "internal", "static", "readonly", "const", "var", "void",
        "string", "bool", "int", "long", "double", "decimal", "char", "object", "new", "null",
        "true", "false", "return", "if", "else", "for", "foreach", "while", "switch", "case",
        "break", "continue", "async", "await", "using", "namespace", "class", "struct", "record",
        "interface", "enum", "get", "set", "init", "this", "base", "is", "not", "and", "or",
        "out", "ref", "in", "default", "try", "catch", "finally", "throw", "nameof", "typeof",
    ];

    /// <summary>
    /// How many chars the code point at <paramref name="i"/> spans - 2 for a surrogate pair (an
    /// emoji in a demo snippet), else 1. Every per-character fall-through advances by this:
    /// tokenizing one <c>char</c> at a time would split the pair across two segments, and
    /// HtmlEncode turns each lone half into U+FFFD (the snippet renders "??").
    /// </summary>
    private static int CharLen(string s, int i) =>
        char.IsHighSurrogate(s[i]) && i + 1 < s.Length ? 2 : 1;

    private static string Render(string code, string? language = null)
    {
        var sb = new StringBuilder(code.Length * 2);
        sb.Append("<span class=\"line\">");
        var segments = language switch
        {
            "yaml" => ScanYaml(code),
            "text" => ScanPlain(code),
            _ => Scan(code),
        };
        foreach (var (text, cls) in segments)
        {
            var parts = text.Split('\n');
            for (var p = 0; p < parts.Length; p++)
            {
                if (p > 0) sb.Append("</span>\n<span class=\"line\">");
                if (parts[p].Length == 0) continue;
                var encoded = WebUtility.HtmlEncode(parts[p]);
                if (cls is null) sb.Append(encoded);
                else sb.Append("<span class=\"").Append(cls).Append("\">").Append(encoded).Append("</span>");
            }
        }
        sb.Append("</span>");
        return sb.ToString();
    }

    /// <summary>
    /// Splits the snippet into (text, token-class) segments. Three modes: CSS (detected first -
    /// comments, at-rules, custom properties, declarations), Razor markup (tags, attributes,
    /// strings, @directives) and C# (keywords, strings, numbers, comments). A snippet with
    /// no markup (no line opening with <c>&lt;</c> or <c>@</c>) is treated as C# end-to-end; otherwise
    /// it starts in markup and drops into C# at <c>@code { … }</c> (brace depth) or <c>@( … )</c> (paren
    /// depth). Most docs snippets are plain C#, so that pure-C# path is the common one.
    /// </summary>
    private static List<(string Text, string? Cls)> Scan(string s)
    {
        if (IsCss(s)) return ScanCss(s);
        if (IsShell(s)) return ScanShell(s);
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;
        var braces = 0;          // @code block depth
        var parens = 0;          // @( … ) expression depth
        var inTemplate = false;  // inside an inline Razor template (@<text> … </text>) within C#
        var templateDepth = 0;   // element depth of that template - 0 again means its root closed
        var pureCs = IsPureCSharp(s);
        var csharp = pureCs;     // pure-C# snippets are in C# mode from the first character

        void Add(int start, int end, string? cls)
        {
            if (end > start) tokens.Add((s[start..end], cls));
        }

        while (i < n)
        {
            if (!csharp)
            {
                // @* … *@ razor comment.
                if (s[i] == '@' && i + 1 < n && s[i + 1] == '*')
                {
                    var end = s.IndexOf("*@", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? n : end + 2;
                    Add(i, end, "tok-comment");
                    i = end;
                    continue;
                }

                // <!-- … --> HTML comment.
                if (s[i] == '<' && i + 3 < n && s[i + 1] == '!' && s[i + 2] == '-' && s[i + 3] == '-')
                {
                    var end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    end = end < 0 ? n : end + 3;
                    Add(i, end, "tok-comment");
                    i = end;
                    continue;
                }

                // <Tag attr="…"> - punctuation + name as tag, attributes until '>'.
                if (s[i] == '<')
                {
                    var isClosing = i + 1 < n && s[i + 1] == '/';
                    var j = i + 1;
                    if (isClosing) j++;
                    while (j < n && (char.IsLetterOrDigit(s[j]) || s[j] is '.' or '-' or '_' or '!')) j++;
                    Add(i, j, "tok-tag");
                    i = j;

                    while (i < n && s[i] != '>')
                    {
                        if (s[i] == '"')
                        {
                            var e = s.IndexOf('"', i + 1);
                            e = e < 0 ? n : e + 1;
                            Add(i, e, "tok-str");
                            i = e;
                            continue;
                        }
                        if (s[i] == '@' || char.IsLetter(s[i]))
                        {
                            var e = s[i] == '@' ? i + 1 : i;
                            while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] is '-' or '_' or ':' or '.')) e++;
                            // Directive attributes (@bind-…, @onclick, @attributes) read as keywords.
                            Add(i, e, s[i] == '@' ? "tok-kw" : "tok-attr");
                            i = e;
                            continue;
                        }
                        Add(i, i + CharLen(s, i), null);
                        i += CharLen(s, i);
                    }

                    if (i < n)
                    {
                        var selfClosing = s[i - 1] == '/';
                        Add(i, i + 1, "tok-tag"); // the '>'
                        i++;
                        // Inside an inline template, tag depth decides when the template's root
                        // element has closed and the scan belongs to C# again.
                        if (inTemplate)
                        {
                            if (isClosing) templateDepth--;
                            else if (!selfClosing) templateDepth++;
                            if (templateDepth <= 0) { inTemplate = false; csharp = true; }
                        }
                    }
                    continue;
                }

                // "…" in text content - e.g. the @t["عربي", "עברית"] args of the RTL demos. Tokenizing
                // them as strings also bidi-ISOLATES each literal (tok-str carries unicode-bidi:
                // plaintext, see app.css); left as plain text, two adjacent RTL literals visually
                // merge across the comma between them and their punctuation scatters.
                if (s[i] == '"')
                {
                    var e = s.IndexOf('"', i + 1);
                    e = e < 0 ? n : e + 1;
                    Add(i, e, "tok-str");
                    i = e;
                    continue;
                }

                // @identifier / @code / @( … ).
                if (s[i] == '@')
                {
                    var e = i + 1;
                    while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] is '_' or '.' or '-')) e++;
                    var isExpr = e < n && s[e] == '(' && e == i + 1;
                    if (isExpr) e++;
                    Add(i, e, "tok-kw");
                    if (s[i..e] == "@code") { csharp = true; braces = 0; }
                    else if (isExpr) { csharp = true; parens = 1; }
                    i = e;
                    continue;
                }

                Add(i, i + CharLen(s, i), null);
                i += CharLen(s, i);
            }
            else
            {
                // // comment to end of line.
                if (s[i] == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    var e = s.IndexOf('\n', i);
                    e = e < 0 ? n : e;
                    Add(i, e, "tok-comment");
                    i = e;
                    continue;
                }

                // @<text> … </text> - an inline Razor template inside C# (a RenderFragment body).
                // The '@' reads as a directive and the scan drops back into markup until the
                // template's root element closes (or self-closes).
                if (s[i] == '@' && i + 1 < n && s[i + 1] == '<')
                {
                    Add(i, i + 1, "tok-kw");
                    i++;
                    csharp = false;
                    inTemplate = true;
                    templateDepth = 0;
                    continue;
                }
                if (s[i] == '"')
                {
                    var e = s.IndexOf('"', i + 1);
                    e = e < 0 ? n : e + 1;
                    Add(i, e, "tok-str");
                    i = e;
                    continue;
                }
                if (char.IsDigit(s[i]))
                {
                    var e = i;
                    while (e < n && (char.IsDigit(s[e]) || s[e] == '.')) e++;
                    Add(i, e, "tok-num");
                    i = e;
                    continue;
                }
                if (char.IsLetter(s[i]) || s[i] == '_')
                {
                    var e = i;
                    while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] == '_')) e++;
                    Add(i, e, Keywords.Contains(s[i..e]) ? "tok-kw" : null);
                    i = e;
                    continue;
                }
                // Brace/paren depth only matters for dropping back to markup in a Razor snippet; a
                // pure-C# snippet stays in C# mode throughout.
                if (!pureCs)
                {
                    if (s[i] == '{' && parens == 0) braces++;
                    if (s[i] == '}' && parens == 0 && --braces <= 0) csharp = false;
                    if (s[i] == '(' && parens > 0) parens++;
                    if (s[i] == ')' && parens > 0 && --parens == 0) csharp = false;
                }
                Add(i, i + CharLen(s, i), null);
                i += CharLen(s, i);
            }
        }

        return tokens;
    }

    // A snippet is a shell/terminal transcript when its first meaningful line is a '#' comment or
    // opens with a known command. The CLI snippets always lead with a '# …' explainer, so the
    // comment check is the one that matters; the command list catches a bare one-liner.
    private static readonly string[] ShellCommands =
        ["blaizio", "dotnet", "cd", "npm", "pnpm", "npx", "yarn", "bun", "git", "tailwindcss", "curl"];

    private static bool IsShell(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var t = line.TrimStart();
            if (t.IsEmpty) continue;
            if (t[0] == '#') return true;
            var space = t.IndexOf(' ');
            var word = (space < 0 ? t : t[..space]).ToString();
            return ShellCommands.Contains(word, StringComparer.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// Shell tokenizer: '#' comments to end of line (comment color), quoted strings, the command
    /// word opening each line/pipeline segment (keyword color), and -flags (attribute color).
    /// Arguments stay plain - a terminal transcript, not a program.
    /// </summary>
    private static List<(string Text, string? Cls)> ScanShell(string s)
    {
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;
        var commandStart = true; // at a spot where a command name may begin (line start, after | && ;)

        void Add(int start, int end, string? cls)
        {
            if (end > start) tokens.Add((s[start..end], cls));
        }

        while (i < n)
        {
            var c = s[i];
            if (c == '#')
            {
                var e = s.IndexOf('\n', i);
                e = e < 0 ? n : e;
                Add(i, e, "tok-comment");
                i = e;
                continue;
            }
            if (c is '"' or '\'')
            {
                var e = s.IndexOf(c, i + 1);
                e = e < 0 ? n : e + 1;
                Add(i, e, "tok-str");
                i = e;
                commandStart = false;
                continue;
            }
            if (c == '-' && i + 1 < n && (s[i + 1] == '-' || char.IsLetter(s[i + 1])))
            {
                var e = i + 1;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] is '-' or '_')) e++;
                Add(i, e, "tok-attr");
                i = e;
                commandStart = false;
                continue;
            }
            if (char.IsLetter(c))
            {
                var e = i;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] is '-' or '_' or '.')) e++;
                Add(i, e, commandStart ? "tok-kw" : null);
                i = e;
                commandStart = false;
                continue;
            }
            if (c == '\n' || (c is '|' or ';' or '&'))
                commandStart = true;
            else if (c != ' ' && c != '\t')
                commandStart = false;
            Add(i, i + CharLen(s, i), null);
            i += CharLen(s, i);
        }

        return tokens;
    }

    /// <summary>
    /// YAML tokenizer: '#' comments, quoted strings, keys (a word followed by ':' at its end,
    /// attribute color), and numbers. Values stay plain - a workflow file, not a program.
    /// </summary>
    private static List<(string Text, string? Cls)> ScanYaml(string s)
    {
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;

        void Add(int start, int end, string? cls)
        {
            if (end > start) tokens.Add((s[start..end], cls));
        }

        while (i < n)
        {
            var c = s[i];
            if (c == '#')
            {
                var e = s.IndexOf('\n', i);
                e = e < 0 ? n : e;
                Add(i, e, "tok-comment");
                i = e;
                continue;
            }
            if (c is '"' or '\'')
            {
                var e = s.IndexOf(c, i + 1);
                e = e < 0 ? n : e + 1;
                Add(i, e, "tok-str");
                i = e;
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var e = i;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] is '-' or '_' or '.')) e++;
                // A key is the word a ':' terminates; anything else (a value, a step name) is plain.
                Add(i, e, e < n && s[e] == ':' ? "tok-attr" : null);
                i = e;
                continue;
            }
            if (char.IsDigit(c))
            {
                var e = i;
                while (e < n && (char.IsDigit(s[e]) || s[e] == '.')) e++;
                Add(i, e, "tok-num");
                i = e;
                continue;
            }
            Add(i, i + CharLen(s, i), null);
            i += CharLen(s, i);
        }

        return tokens;
    }

    /// <summary>Plain text (directory trees, transcripts): only '#' end-of-line comments color.</summary>
    private static List<(string Text, string? Cls)> ScanPlain(string s)
    {
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;
        while (i < n)
        {
            if (s[i] == '#')
            {
                var e = s.IndexOf('\n', i);
                e = e < 0 ? n : e;
                tokens.Add((s[i..e], "tok-comment"));
                i = e;
                continue;
            }
            var next = s.IndexOf('#', i);
            next = next < 0 ? n : next;
            tokens.Add((s[i..next], null));
            i = next;
        }
        return tokens;
    }

    // A snippet is CSS when its first meaningful line opens like a stylesheet: a /* comment, a CSS
    // at-rule (@import/@theme/@layer/…, distinct from Razor's @directives), :root, a --custom
    // property, or a class selector line ending in '{'.
    private static bool IsCss(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var t = line.TrimStart();
            if (t.IsEmpty) continue;
            if (t.StartsWith("/*")) return true;
            if (t.StartsWith(":root") || t.StartsWith("--")) return true;
            if (t[0] == '@')
                return t.StartsWith("@import") || t.StartsWith("@theme") || t.StartsWith("@layer") ||
                       t.StartsWith("@media") || t.StartsWith("@custom-variant") || t.StartsWith("@keyframes") ||
                       t.StartsWith("@apply") || t.StartsWith("@source");
            if (t[0] == '.') return t.TrimEnd().EndsWith("{");
            return false;
        }
        return false;
    }

    /// <summary>
    /// CSS tokenizer: /* comments */, strings, at-rule names (keyword color), --custom-properties
    /// (attribute color), property names at declaration starts (attribute color), numbers.
    /// Selectors and value functions stay plain.
    /// </summary>
    private static List<(string Text, string? Cls)> ScanCss(string s)
    {
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;
        var declStart = true; // at a spot where a property name may begin ('{', ';', or line start)

        void Add(int start, int end, string? cls)
        {
            if (end > start) tokens.Add((s[start..end], cls));
        }

        while (i < n)
        {
            var c = s[i];
            if (c == '/' && i + 1 < n && s[i + 1] == '*')
            {
                var e = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                e = e < 0 ? n : e + 2;
                Add(i, e, "tok-comment");
                i = e;
                continue;
            }
            if (c is '"' or '\'')
            {
                var e = s.IndexOf(c, i + 1);
                e = e < 0 ? n : e + 1;
                Add(i, e, "tok-str");
                i = e;
                continue;
            }
            if (c == '@' && i + 1 < n && char.IsLetter(s[i + 1]))
            {
                var e = i + 1;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] == '-')) e++;
                Add(i, e, "tok-kw");
                i = e;
                continue;
            }
            if (c == '-' && i + 1 < n && s[i + 1] == '-')
            {
                var e = i + 2;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] == '-' || s[e] == '_')) e++;
                Add(i, e, "tok-attr");
                i = e;
                declStart = false;
                continue;
            }
            if (declStart && char.IsLetter(c))
            {
                var e = i;
                while (e < n && (char.IsLetterOrDigit(s[e]) || s[e] == '-')) e++;
                var probe = e;
                while (probe < n && s[probe] == ' ') probe++;
                // property name only when directly followed by ':' (selectors keep going with
                // other characters - '.', '[', spaces before '{', …)
                Add(i, e, probe < n && s[probe] == ':' ? "tok-attr" : null);
                i = e;
                declStart = false;
                continue;
            }
            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(s[i + 1])))
            {
                var e = i;
                while (e < n && (char.IsDigit(s[e]) || s[e] == '.')) e++;
                Add(i, e, "tok-num");
                i = e;
                declStart = false;
                continue;
            }
            if (c is '{' or ';' or '\n') declStart = true;
            else if (c != ' ' && c != '\t') declStart = false;
            Add(i, i + CharLen(s, i), null);
            i += CharLen(s, i);
        }

        return tokens;
    }

    // A snippet is plain C# when no line opens (after indentation) with a Razor tag '<' or an '@'
    // directive - then there's no markup to track and the whole thing tokenizes as C#. Mixed snippets
    // (markup + code) keep the markup-first path. C# generics like List<T> never trip this: their '<'
    // is mid-line, not at the start.
    private static bool IsPureCSharp(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var trimmed = line.TrimStart();
            if (trimmed.IsEmpty) continue;
            if (trimmed[0] is '<' or '@') return false;
        }

        return true;
    }
}
