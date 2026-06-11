namespace Blazeo;

/// <summary>
/// Reading / layout direction. Cascaded by <see cref="BlazeDirectionProvider"/> and consumed
/// by primitives that care about horizontal navigation (menus, tabs, sliders…).
/// </summary>
public enum Direction
{
    /// <summary>Left-to-right (the default).</summary>
    Ltr,

    /// <summary>Right-to-left.</summary>
    Rtl,
}

/// <summary>
/// Orientation of a composite widget. Surfaced as <c>data-orientation</c> and, where semantically
/// required, <c>aria-orientation</c>.
/// </summary>
public enum Orientation
{
    /// <summary>Laid out along the horizontal axis (the default).</summary>
    Horizontal,

    /// <summary>Laid out along the vertical axis.</summary>
    Vertical,
}

/// <summary>
/// Lower-cased string forms used in <c>data-*</c> / <c>aria-*</c> attributes, matching the Radix
/// conventions the styled layer targets (e.g. <c>data-[orientation=vertical]</c>).
/// </summary>
public static class BlazeEnumExtensions
{
    public static string ToAttribute(this Direction direction) => direction == Direction.Rtl ? "rtl" : "ltr";

    public static string ToAttribute(this Orientation orientation) =>
        orientation == Orientation.Vertical ? "vertical" : "horizontal";
}
