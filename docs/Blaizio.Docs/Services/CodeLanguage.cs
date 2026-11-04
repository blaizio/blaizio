namespace Blaizio.Docs.Services;

/// <summary>
/// The language a docs snippet highlights as, for <c>BzCodeBlockContent Language</c>. The Code Block
/// highlights only the language it is given, and the docs' snippets don't name theirs, so the
/// content decides.
/// </summary>
/// <remarks>
/// Inputs are the docs' own embedded snippets - trusted, short, and finite. The sniff is tuned to
/// them, not to arbitrary code.
/// </remarks>
public static class CodeLanguage
{
    /// <summary>
    /// The language name for <paramref name="code"/>, or null for plain text. A pinned
    /// <paramref name="language"/> wins when it names a built-in language or one of its aliases
    /// (<c>yaml</c>, <c>bash</c>, <c>css</c>, <c>razor</c>, <c>cs</c>, ...), and <c>text</c> keeps the
    /// code plain; any other name falls back to the content: CSS, a shell transcript (a console
    /// session when its commands sit behind <c>$ </c> prompts), JSON, then C# when no line opens
    /// with markup, else Razor.
    /// </summary>
    public static string? Detect(string code, string? language = null)
    {
        if (string.Equals(language, "text", StringComparison.OrdinalIgnoreCase)) return null;
        if (SyntaxText.GetLanguageName(language) is { } pinned) return pinned;
        if (IsCss(code)) return "css";
        if (IsShell(code)) return HasPrompt(code) ? "console" : "bash";
        if (IsJson(code)) return "json";
        return IsPureCSharp(code) ? "csharp" : "razor";
    }

    // A snippet is a shell/terminal transcript when its first meaningful line is a '#' comment, a
    // '$ ' prompt, or opens with a known command. The CLI snippets always lead with a '# …'
    // explainer, so the comment check is the one that matters; the command list catches a bare
    // one-liner.
    private static readonly string[] ShellCommands =
        ["blaizio", "dotnet", "cd", "npm", "pnpm", "npx", "yarn", "bun", "git", "tailwindcss", "curl"];

    private static bool IsShell(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var t = line.TrimStart();
            if (t.IsEmpty) continue;
            if (t[0] == '#' || t.StartsWith("$ ")) return true;
            var space = t.IndexOf(' ');
            var word = (space < 0 ? t : t[..space]).ToString();
            return ShellCommands.Contains(word, StringComparer.Ordinal);
        }
        return false;
    }

    // Commands behind '$ ' prompts make a console session: the prompt reads as metadata, the
    // command as bash, and the lines without a prompt as the commands' output.
    private static bool HasPrompt(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
            if (line.TrimStart().StartsWith("$ ")) return true;
        return false;
    }

    // A snippet is JSON when its first meaningful line - past any // comments (JSONC) - opens an
    // object or array: '{' or '[' followed by nothing, a quote or another bracket. A C# attribute
    // list ([Parameter]) has a name after its '[', so it never trips this.
    private static bool IsJson(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var t = line.Trim();
            if (t.IsEmpty || t.StartsWith("//")) continue;
            if (t[0] is not ('{' or '[')) return false;
            var rest = t[1..].TrimStart();
            return rest.IsEmpty || rest[0] is '"' or '{' or '[' or '}' or ']';
        }
        return false;
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

    // A snippet is plain C# when no line opens (after indentation) with a Razor tag or an '@'
    // directive - then there's no markup to track and the whole thing reads as C#. Mixed snippets
    // (markup + code) read as Razor. C# generics like List<T> never trip this: their '<' is
    // mid-line. Neither does a '<' that opens a line with no tag name after it - the relational
    // pattern of a switch arm (`< 1 => ...`).
    private static bool IsPureCSharp(string code)
    {
        foreach (var line in code.AsSpan().EnumerateLines())
        {
            var t = line.TrimStart();
            if (t.IsEmpty) continue;
            if (t[0] == '@') return false;
            if (t[0] == '<' && t.Length > 1 && (char.IsLetter(t[1]) || t[1] is '/' or '!')) return false;
        }

        return true;
    }
}
