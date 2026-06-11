namespace Blazeo.Ui;

/// <summary>
/// The new-york v4 toggle classes — shadcn's <c>toggleVariants</c> — shared by <see cref="Toggle"/>
/// and <see cref="ToggleGroupItem"/>. Lives in a .cs file, so the Tailwind source globs must include
/// <c>**/*.cs</c> (the docs' Styles/app.css shows the wiring).
/// </summary>
internal static class ToggleStyles
{
    public const string Base =
        "inline-flex items-center justify-center gap-2 rounded-md text-sm font-medium whitespace-nowrap " +
        "outline-none transition-[color,box-shadow] hover:bg-muted hover:text-muted-foreground " +
        "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 " +
        "disabled:pointer-events-none disabled:opacity-50 data-[state=on]:bg-accent data-[state=on]:text-accent-foreground " +
        "aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 " +
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4";

    public static string Variant(ToggleVariant variant) => variant switch
    {
        ToggleVariant.Outline =>
            "border border-input bg-transparent shadow-xs hover:bg-accent hover:text-accent-foreground",
        _ => "bg-transparent",
    };

    public static string Size(ToggleSize size) => size switch
    {
        ToggleSize.Sm => "h-8 min-w-8 px-1.5",
        ToggleSize.Lg => "h-10 min-w-10 px-2.5",
        _ => "h-9 min-w-9 px-2",
    };
}
