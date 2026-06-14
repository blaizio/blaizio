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
/// Tri-state of a checkbox (<c>boolean | 'indeterminate'</c>): the
/// <see cref="Indeterminate"/> middle state is surfaced as <c>aria-checked="mixed"</c> and
/// <c>data-state="indeterminate"</c>, and resolves to checked on the next user toggle.
/// </summary>
public enum CheckedState
{
    /// <summary>Not checked (the default).</summary>
    Unchecked,

    /// <summary>Checked.</summary>
    Checked,

    /// <summary>Neither fully checked nor unchecked (e.g. a partially-selected "select all").</summary>
    Indeterminate,
}

/// <summary>
/// Lower-cased string forms used in <c>data-*</c> / <c>aria-*</c> attributes, matching the
/// conventions the styled layer targets (e.g. <c>data-[orientation=vertical]</c>).
/// </summary>
public static class BlazeEnumExtensions
{
    public static string ToAttribute(this Direction direction) => direction == Direction.Rtl ? "rtl" : "ltr";

    public static string ToAttribute(this Orientation orientation) =>
        orientation == Orientation.Vertical ? "vertical" : "horizontal";

    /// <summary>The <c>data-state</c> string for a checkbox/indicator.</summary>
    public static string ToAttribute(this CheckedState state) => state switch
    {
        CheckedState.Checked => "checked",
        CheckedState.Indeterminate => "indeterminate",
        _ => "unchecked",
    };
}
