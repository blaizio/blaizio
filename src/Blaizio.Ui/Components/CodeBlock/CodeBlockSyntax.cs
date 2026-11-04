using System.Text;

namespace Blaizio.Ui;

/// <summary>
/// Renders tokenized lines (<see cref="SyntaxText"/>) for <c>BzCodeBlockContent Language="..."</c>:
/// every run that is not plain text becomes a span whose <c>data-token</c> names its kind
/// (<c>keyword</c>, <c>string</c>, <c>type</c>, ...). The colors are not in the spans - the content's
/// classes color each <c>data-token</c> from the theme - so a line is one small piece of markup,
/// built once and reused while the line is unchanged.
/// </summary>
internal static class CodeBlockSyntax
{
    /// <summary>The markup of one line: its runs, HTML-escaped, the tokens wrapped in spans.</summary>
    public static string Markup(SyntaxLine line)
    {
        var sb = new StringBuilder(line.Text.Length + 32 * line.Runs.Count);
        foreach (var run in line.Runs)
        {
            if (Token(run.Kind) is not { } token)
            {
                Escape(sb, run.Text);
                continue;
            }
            sb.Append("<span data-token=\"").Append(token).Append("\">");
            Escape(sb, run.Text);
            sb.Append("</span>");
        }
        return sb.ToString();
    }

    // The data-token value of a kind; null for plain text.
    private static string? Token(SyntaxKind kind) => kind switch
    {
        SyntaxKind.Comment => "comment",
        SyntaxKind.Keyword => "keyword",
        SyntaxKind.String => "string",
        SyntaxKind.Escape => "escape",
        SyntaxKind.Number => "number",
        SyntaxKind.Constant => "constant",
        SyntaxKind.Type => "type",
        SyntaxKind.Function => "function",
        SyntaxKind.Property => "property",
        SyntaxKind.Attribute => "attribute",
        SyntaxKind.Tag => "tag",
        SyntaxKind.Variable => "variable",
        SyntaxKind.Meta => "meta",
        SyntaxKind.Inserted => "inserted",
        SyntaxKind.Deleted => "deleted",
        SyntaxKind.Heading => "heading",
        SyntaxKind.Emphasis => "emphasis",
        SyntaxKind.Strong => "strong",
        SyntaxKind.Link => "link",
        _ => null,
    };

    // Text content needs only &, < and > escaped.
    private static void Escape(StringBuilder sb, string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var entity = text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => null,
            };
            if (entity is null) continue;
            sb.Append(text, start, i - start).Append(entity);
            start = i + 1;
        }
        sb.Append(text, start, text.Length - start);
    }
}
