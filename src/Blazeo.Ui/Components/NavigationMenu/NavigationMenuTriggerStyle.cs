namespace Blazeo.Ui;

/// <summary>
/// The shared classes a <see cref="BzNavigationMenuTrigger"/> uses. Exposed so a
/// <see cref="BzNavigationMenuLink"/> that should look like a trigger (a top-level link with no
/// dropdown) can reuse the same look: <c>Class="@NavigationMenuTriggerStyle.Classes"</c>.
/// </summary>
public static class NavigationMenuTriggerStyle
{
    /// <summary>The trigger's Tailwind classes.</summary>
    public const string Classes =
        "group inline-flex h-9 w-max items-center justify-center rounded-md bg-background px-4 py-2 text-sm font-medium " +
        "transition-[color,box-shadow] outline-none hover:bg-accent hover:text-accent-foreground " +
        "focus:bg-accent focus:text-accent-foreground focus-visible:ring-ring/50 focus-visible:ring-[3px] focus-visible:outline-1 " +
        "disabled:pointer-events-none disabled:opacity-50 " +
        "data-[state=open]:bg-accent/50 data-[state=open]:text-accent-foreground " +
        "data-[state=open]:hover:bg-accent data-[state=open]:focus:bg-accent";
}
