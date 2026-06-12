using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace Blazeo.Docs.Services;

/// <summary>Turns a demo snippet into colored, line-numbered HTML.</summary>
public interface ICodeHighlighter
{
    /// <summary>
    /// Highlighted markup: token spans (<c>tok-*</c>, colored by the <c>--code*</c> palette) inside
    /// one <c>span.line</c> per line (numbered by a CSS counter - see Styles/app.css).
    /// </summary>
    MarkupString Highlight(string code);
}

/// <summary>
/// A tiny Razor tokenizer replacing the highlight.js stack. The docs' snippets are short and
/// known-shape, so a single pass in C# produces the colors client-side JS used to - generated once
/// per distinct snippet (cached), shipped with zero script payload.
/// </summary>
internal sealed class CodeHighlighter : ICodeHighlighter
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public MarkupString Highlight(string code) =>
        new(_cache.GetOrAdd(code.ReplaceLineEndings("\n"), Render));

    private static readonly HashSet<string> Keywords =
    [
        "private", "public", "protected", "internal", "static", "readonly", "const", "var", "void",
        "string", "bool", "int", "long", "double", "decimal", "char", "object", "new", "null",
        "true", "false", "return", "if", "else", "for", "foreach", "while", "switch", "case",
        "break", "continue", "async", "await", "using", "namespace", "class", "struct", "record",
        "interface", "enum", "get", "set", "init", "this", "base", "is", "not", "and", "or",
        "out", "ref", "in", "default", "try", "catch", "finally", "throw", "nameof", "typeof",
    ];

    private static string Render(string code)
    {
        var sb = new StringBuilder(code.Length * 2);
        sb.Append("<span class=\"line\">");
        foreach (var (text, cls) in Scan(code))
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
    /// Splits the snippet into (text, token-class) segments. Two modes: Razor markup (tags,
    /// attributes, strings, @directives) and C# (entered at <c>@code { … }</c> by brace depth, or
    /// inside a <c>@( … )</c> expression by paren depth).
    /// </summary>
    private static List<(string Text, string? Cls)> Scan(string s)
    {
        var tokens = new List<(string, string?)>();
        int i = 0, n = s.Length;
        var braces = 0;     // @code block depth
        var parens = 0;     // @( … ) expression depth
        var csharp = false; // true while inside either of the above

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

                // <Tag attr="…"> - punctuation + name as tag, attributes until '>'.
                if (s[i] == '<')
                {
                    var j = i + 1;
                    if (j < n && s[j] == '/') j++;
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
                        Add(i, i + 1, null);
                        i++;
                    }

                    if (i < n) { Add(i, i + 1, "tok-tag"); i++; } // the '>'
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

                Add(i, i + 1, null);
                i++;
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
                if (s[i] == '{' && parens == 0) braces++;
                if (s[i] == '}' && parens == 0 && --braces <= 0) csharp = false;
                if (s[i] == '(' && parens > 0) parens++;
                if (s[i] == ')' && parens > 0 && --parens == 0) csharp = false;
                Add(i, i + 1, null);
                i++;
            }
        }

        return tokens;
    }
}
