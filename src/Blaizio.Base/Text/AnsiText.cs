using System.Text;

namespace Blaizio;

/// <summary>
/// Splits terminal output that carries ANSI escape sequences into styled runs, one list per line -
/// the parsing half of rendering colored command output; the styled layer decides what each color
/// looks like.
/// </summary>
/// <remarks>
/// <para>SGR sequences (<c>ESC[…m</c>) become <see cref="AnsiStyle"/>s: the 16 named colors, the
/// 256-color palette and 24-bit colors, foreground and background, plus bold, dim, italic,
/// underline, inverse and strikethrough. Every other escape (cursor movement, erase, window titles,
/// hyperlinks) is dropped and its visible text kept.</para>
/// <para>A carriage return rewinds its line, so a progress bar that redraws itself shows only its
/// last frame. Styles do not carry from one line to the next: a line renders the same whether the
/// output arrives in one piece or streams in, and an unfinished sequence at the end of streaming
/// text is simply left out until the rest of it arrives.</para>
/// </remarks>
public static class AnsiText
{
    private const char Escape = '\u001b';

    /// <summary>Parses <paramref name="text"/> into one list of runs per line (<c>\n</c> or <c>\r\n</c> separated).</summary>
    public static IReadOnlyList<IReadOnlyList<AnsiRun>> ParseLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new IReadOnlyList<AnsiRun>[lines.Length];
        for (var i = 0; i < lines.Length; i++) result[i] = ParseLine(lines[i]);
        return result;
    }

    /// <summary>Parses a single line into its styled runs. A line without escapes is one unstyled run.</summary>
    public static IReadOnlyList<AnsiRun> ParseLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var runs = new List<AnsiRun>();
        var text = new StringBuilder();
        var style = AnsiStyle.Default;
        var rewind = false;

        void Flush()
        {
            if (text.Length == 0) return;
            runs.Add(new AnsiRun(text.ToString(), style));
            text.Clear();
        }

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == Escape)
            {
                if (i + 1 >= line.Length) break;
                var kind = line[i + 1];
                if (kind == '[')
                {
                    // CSI: parameter and intermediate bytes, then one final byte in @..~.
                    var end = i + 2;
                    while (end < line.Length && (line[end] < '@' || line[end] > '~')) end++;
                    if (end >= line.Length) break;
                    if (line[end] == 'm')
                    {
                        Flush();
                        style = Apply(style, line.AsSpan(i + 2, end - i - 2));
                    }
                    i = end;
                }
                else if (kind == ']')
                {
                    // OSC (titles, hyperlinks): runs to BEL or ESC \.
                    var end = i + 2;
                    while (end < line.Length && line[end] != '\u0007' && line[end] != Escape) end++;
                    if (end >= line.Length) break;
                    i = line[end] == Escape ? end + 1 : end;
                }
                else
                {
                    // A two-character escape (ESC 7, ESC c, ...).
                    i++;
                }
                continue;
            }

            if (c == '\r')
            {
                // The cursor goes back to the start; what is written next replaces the line. A
                // trailing \r writes nothing, so the line stays as it was.
                rewind = true;
                continue;
            }

            // BEL, backspace and other controls have no glyph. Tabs stay.
            if (char.IsControl(c) && c != '\t') continue;

            if (rewind)
            {
                runs.Clear();
                text.Clear();
                rewind = false;
            }
            text.Append(c);
        }

        Flush();
        return runs;
    }

    private static AnsiStyle Apply(AnsiStyle style, ReadOnlySpan<char> parameters)
    {
        // ESC[m resets; an empty field counts as 0, so ESC[;31m resets and then sets red.
        if (parameters.IsEmpty) return AnsiStyle.Default;

        var groups = parameters.ToString().Split(';');
        for (var k = 0; k < groups.Length; k++)
        {
            // A group can carry colon sub-parameters (38:2::r:g:b), the self-contained extended form.
            var sub = groups[k].Split(':');
            var p = Number(sub[0]);
            switch (p)
            {
                case 0: style = AnsiStyle.Default; break;
                case 1: style = style with { Decoration = style.Decoration | AnsiDecoration.Bold }; break;
                case 2: style = style with { Decoration = style.Decoration | AnsiDecoration.Dim }; break;
                case 3: style = style with { Decoration = style.Decoration | AnsiDecoration.Italic }; break;
                case 4: style = style with { Decoration = style.Decoration | AnsiDecoration.Underline }; break;
                case 7: style = style with { Decoration = style.Decoration | AnsiDecoration.Inverse }; break;
                case 9: style = style with { Decoration = style.Decoration | AnsiDecoration.Strikethrough }; break;
                case 22: style = style with { Decoration = style.Decoration & ~(AnsiDecoration.Bold | AnsiDecoration.Dim) }; break;
                case 23: style = style with { Decoration = style.Decoration & ~AnsiDecoration.Italic }; break;
                case 24: style = style with { Decoration = style.Decoration & ~AnsiDecoration.Underline }; break;
                case 27: style = style with { Decoration = style.Decoration & ~AnsiDecoration.Inverse }; break;
                case 29: style = style with { Decoration = style.Decoration & ~AnsiDecoration.Strikethrough }; break;
                case >= 30 and <= 37: style = style with { Foreground = AnsiColor.Named(p - 30) }; break;
                case 39: style = style with { Foreground = null }; break;
                case >= 40 and <= 47: style = style with { Background = AnsiColor.Named(p - 40) }; break;
                case 49: style = style with { Background = null }; break;
                case >= 90 and <= 97: style = style with { Foreground = AnsiColor.Named(p - 90 + 8) }; break;
                case >= 100 and <= 107: style = style with { Background = AnsiColor.Named(p - 100 + 8) }; break;
                case 38 or 48:
                    var color = sub.Length > 1 ? ExtendedFromColons(sub) : ExtendedFromSemicolons(groups, ref k);
                    if (color is { } extended)
                        style = p == 38 ? style with { Foreground = extended } : style with { Background = extended };
                    break;
            }
        }
        return style;
    }

    /// <summary>The colon form: <c>38:5:n</c>, <c>38:2:r:g:b</c> or <c>38:2:&lt;color space&gt;:r:g:b</c>.</summary>
    private static AnsiColor? ExtendedFromColons(string[] sub) => Number(sub[1]) switch
    {
        5 when sub.Length >= 3 => AnsiColor.FromPalette(Number(sub[2])),
        2 when sub.Length >= 6 => AnsiColor.Rgb(Channel(sub[3]), Channel(sub[4]), Channel(sub[5])),
        2 when sub.Length == 5 => AnsiColor.Rgb(Channel(sub[2]), Channel(sub[3]), Channel(sub[4])),
        _ => null,
    };

    /// <summary>The semicolon form: reads the <c>5;n</c> or <c>2;r;g;b</c> groups after a 38/48, advancing past them.</summary>
    private static AnsiColor? ExtendedFromSemicolons(string[] groups, ref int k)
    {
        if (k + 1 >= groups.Length) return null;
        switch (Number(groups[k + 1]))
        {
            case 5 when k + 2 < groups.Length:
                k += 2;
                return AnsiColor.FromPalette(Number(groups[k]));
            case 2 when k + 4 < groups.Length:
                var color = AnsiColor.Rgb(Channel(groups[k + 2]), Channel(groups[k + 3]), Channel(groups[k + 4]));
                k += 4;
                return color;
            default:
                // A malformed tail: the rest of the sequence cannot be read reliably.
                k = groups.Length;
                return null;
        }
    }

    private static int Number(string field) =>
        int.TryParse(field, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? Math.Min(n, 9999)
            : 0;

    private static byte Channel(string field) => (byte)Math.Clamp(Number(field), 0, 255);
}

/// <summary>A stretch of text with one <see cref="AnsiStyle"/>, produced by <see cref="AnsiText"/>.</summary>
/// <param name="Text">The visible text, escapes removed.</param>
/// <param name="Style">How the text is drawn.</param>
public readonly record struct AnsiRun(string Text, AnsiStyle Style);

/// <summary>The SGR state of a run: colors and decorations. <see cref="Default"/> is plain text.</summary>
/// <param name="Foreground">The text color, or <see langword="null"/> for the default color.</param>
/// <param name="Background">The background color, or <see langword="null"/> for none.</param>
/// <param name="Decoration">Bold, dim, italic, underline and strikethrough flags.</param>
public readonly record struct AnsiStyle(AnsiColor? Foreground, AnsiColor? Background, AnsiDecoration Decoration)
{
    /// <summary>Plain text: default colors, no decoration.</summary>
    public static AnsiStyle Default => default;

    /// <summary>Whether this is plain text.</summary>
    public bool IsDefault => this == default;
}

/// <summary>The text decorations an SGR sequence can switch on.</summary>
[Flags]
public enum AnsiDecoration
{
    /// <summary>No decoration.</summary>
    None = 0,

    /// <summary>Bold (SGR 1).</summary>
    Bold = 1,

    /// <summary>Dim or faint (SGR 2).</summary>
    Dim = 2,

    /// <summary>Italic (SGR 3).</summary>
    Italic = 4,

    /// <summary>Underline (SGR 4).</summary>
    Underline = 8,

    /// <summary>Strikethrough (SGR 9).</summary>
    Strikethrough = 16,

    /// <summary>
    /// Inverse video (SGR 7): the foreground and background swap - how test runners draw their
    /// PASS / FAIL badges.
    /// </summary>
    Inverse = 32,
}

/// <summary>
/// A terminal color: one of the 16 named colors, which a style maps onto its own palette, or an exact
/// color from the 256-color palette or a 24-bit sequence.
/// </summary>
public readonly record struct AnsiColor
{
    // The xterm defaults, for consumers that render named colors without a palette of their own.
    private static readonly (byte R, byte G, byte B)[] NamedDefaults =
    [
        (0, 0, 0), (205, 49, 49), (13, 188, 121), (229, 229, 16),
        (36, 114, 200), (188, 63, 188), (17, 168, 205), (229, 229, 229),
        (102, 102, 102), (241, 76, 76), (35, 209, 139), (245, 245, 67),
        (59, 142, 234), (214, 112, 214), (41, 184, 219), (255, 255, 255),
    ];

    private AnsiColor(int index, byte r, byte g, byte b)
    {
        Index = index;
        R = r;
        G = g;
        B = b;
    }

    /// <summary>
    /// The named color: 0-7 black, red, green, yellow, blue, magenta, cyan, white; 8-15 their bright
    /// forms. -1 for an exact color (<see cref="R"/>, <see cref="G"/>, <see cref="B"/>).
    /// </summary>
    public int Index { get; }

    /// <summary>Whether this is one of the 16 named colors.</summary>
    public bool IsNamed => Index >= 0;

    /// <summary>The red channel (the xterm default for a named color).</summary>
    public byte R { get; }

    /// <summary>The green channel (the xterm default for a named color).</summary>
    public byte G { get; }

    /// <summary>The blue channel (the xterm default for a named color).</summary>
    public byte B { get; }

    /// <summary>A named color, 0-15.</summary>
    public static AnsiColor Named(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 15);
        var (r, g, b) = NamedDefaults[index];
        return new AnsiColor(index, r, g, b);
    }

    /// <summary>An exact 24-bit color.</summary>
    public static AnsiColor Rgb(byte r, byte g, byte b) => new(-1, r, g, b);

    /// <summary>
    /// Entry <paramref name="n"/> of the 256-color palette: 0-15 are the named colors, 16-231 the
    /// 6x6x6 color cube, 232-255 the gray ramp. Out-of-range values clamp.
    /// </summary>
    public static AnsiColor FromPalette(int n)
    {
        n = Math.Clamp(n, 0, 255);
        if (n < 16) return Named(n);
        if (n >= 232)
        {
            var gray = (byte)(8 + (n - 232) * 10);
            return Rgb(gray, gray, gray);
        }

        n -= 16;
        return Rgb(Level(n / 36), Level(n / 6 % 6), Level(n % 6));

        static byte Level(int step) => step == 0 ? (byte)0 : (byte)(55 + step * 40);
    }

    /// <summary>The color as a CSS value, e.g. <c>rgb(36 114 200)</c>.</summary>
    public string ToCss() => $"rgb({R} {G} {B})";
}
