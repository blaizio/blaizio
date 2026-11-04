namespace Blaizio.Ui;

/// <summary>
/// Draws parsed ANSI runs (<see cref="AnsiText"/>) in the theme's own colors, for
/// <c>BzCodeBlockContent Language="ansi"</c>. Red, green, yellow and blue are the destructive,
/// success, warning and info tokens - darkened in light mode exactly like the alert's text, so
/// terminal output reads at the same contrast - magenta and cyan are mixes of those, and black and
/// white follow the muted and main text colors. A bright color draws like its normal pair; 256-color
/// and 24-bit colors keep the exact color the program asked for.
/// </summary>
internal static class CodeBlockAnsi
{
    // Each class string is a whole literal so Tailwind's scanner sees it. Index = ANSI color 0-7.
    private static readonly string[] Text =
    [
        "text-muted-foreground",
        "text-destructive",
        "text-[color:color-mix(in_oklab,var(--success),black_20%)] dark:text-success",
        "text-[color:color-mix(in_oklab,var(--warning),black_20%)] dark:text-warning",
        "text-[color:color-mix(in_oklab,var(--info),black_20%)] dark:text-info",
        "text-[color:color-mix(in_oklab,color-mix(in_oklch,var(--destructive),var(--info)),black_15%)] dark:text-[color:color-mix(in_oklch,var(--destructive),var(--info))]",
        "text-[color:color-mix(in_oklab,color-mix(in_oklch,var(--success),var(--info)),black_20%)] dark:text-[color:color-mix(in_oklch,var(--success),var(--info))]",
        "text-foreground",
    ];

    private static readonly string[] Background =
    [
        "bg-foreground/10",
        "bg-destructive/15",
        "bg-success/15",
        "bg-warning/20",
        "bg-info/15",
        "bg-[color-mix(in_oklch,var(--destructive),var(--info))]/15",
        "bg-[color-mix(in_oklch,var(--success),var(--info))]/15",
        "bg-foreground/15",
    ];

    // Inverse video turns the text color into a fill - how test runners draw PASS / FAIL badges -
    // so each color pairs with the label color its token was made for.
    private static readonly string[] Inverse =
    [
        "bg-muted-foreground text-background",
        "bg-destructive text-destructive-foreground",
        "bg-success text-success-foreground",
        "bg-warning text-warning-foreground",
        "bg-info text-info-foreground",
        "bg-[color-mix(in_oklch,var(--destructive),var(--info))] text-background",
        "bg-[color-mix(in_oklch,var(--success),var(--info))] text-background",
        "bg-foreground text-background",
    ];

    /// <summary>The classes and inline style for a run; both <see langword="null"/> for plain text.</summary>
    public static (string? Classes, string? Style) Render(AnsiStyle style)
    {
        if (style.IsDefault) return (null, null);

        var classes = new List<string>(4);
        var css = new List<string>(2);
        var (foreground, background) = (style.Foreground, style.Background);

        if (style.Decoration.HasFlag(AnsiDecoration.Inverse))
        {
            switch (foreground)
            {
                case null: classes.Add("bg-foreground text-background"); break;
                case { IsNamed: true } named: classes.Add(Inverse[named.Index % 8]); break;
                case { } exact:
                    css.Add($"background-color: {exact.ToCss()}");
                    classes.Add("text-background");
                    break;
            }
        }
        else
        {
            switch (foreground)
            {
                case { IsNamed: true } named: classes.Add(Text[named.Index % 8]); break;
                case { } exact: css.Add($"color: {exact.ToCss()}"); break;
            }
            switch (background)
            {
                case { IsNamed: true } named: classes.Add(Background[named.Index % 8]); break;
                case { } exact: css.Add($"background-color: {exact.ToCss()}"); break;
            }
        }

        if (style.Decoration.HasFlag(AnsiDecoration.Bold)) classes.Add("font-bold");
        if (style.Decoration.HasFlag(AnsiDecoration.Dim)) classes.Add("opacity-70");
        if (style.Decoration.HasFlag(AnsiDecoration.Italic)) classes.Add("italic");
        if (style.Decoration.HasFlag(AnsiDecoration.Underline)) classes.Add("underline");
        if (style.Decoration.HasFlag(AnsiDecoration.Strikethrough)) classes.Add("line-through");

        return (classes.Count == 0 ? null : string.Join(' ', classes), css.Count == 0 ? null : string.Join("; ", css));
    }
}
