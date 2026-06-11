namespace Blazeo.Ui;

/// <summary>
/// Look settings a <see cref="ToggleGroup"/> cascades so its <see cref="ToggleGroupItem"/>s render
/// consistently (shadcn's ToggleGroupContext). A <see langword="null"/> member means "not set on
/// the group" — the item falls back to its own parameter.
/// </summary>
/// <param name="Variant">The group-level variant, if any.</param>
/// <param name="Size">The group-level size, if any.</param>
public sealed record ToggleGroupStyle(ToggleVariant? Variant, ToggleSize? Size);
