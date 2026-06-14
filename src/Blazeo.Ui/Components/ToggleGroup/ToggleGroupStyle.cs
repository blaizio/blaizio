namespace Blazeo.Ui;

/// <summary>
/// Look settings a <see cref="ToggleGroup"/> cascades so its <see cref="ToggleGroupItem"/>s render
/// consistently (the toggle group's shared context). A <see langword="null"/> member means "not set on
/// the group" - the item falls back to its own parameter.
/// </summary>
/// <param name="Variant">The group-level variant, if any.</param>
/// <param name="Size">The group-level size, if any.</param>
/// <param name="Spacing">The gap between items in spacing units; <c>0</c> joins them into a segmented control.</param>
public sealed record ToggleGroupStyle(ToggleVariant? Variant, ToggleSize? Size, int Spacing);
