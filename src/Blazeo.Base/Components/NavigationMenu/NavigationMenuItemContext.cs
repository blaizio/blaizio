namespace Blazeo;

/// <summary>The id a <see cref="BaseNavigationMenuItem"/> cascades to its trigger and content so they
/// share the same open-state slot in the menu.</summary>
public sealed record NavigationMenuItemContext(string Id);
