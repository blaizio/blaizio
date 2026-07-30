namespace Blaizio;

/// <summary>
/// Reading / layout direction. Cascaded by <see cref="BaseDirectionProvider"/> and consumed
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
/// How many items a composite widget (accordion, toggle group, tree, select, combobox…) lets the
/// user have selected/expanded at once. Widgets that require a selection treat
/// <see cref="None"/> as <see cref="Single"/>.
/// </summary>
public enum SelectionMode
{
    /// <summary>Selection is disabled.</summary>
    None,

    /// <summary>At most one item at a time (the default).</summary>
    Single,

    /// <summary>Any number of items.</summary>
    Multiple,
}

/// <summary>
/// A side of a reference element that a floating surface (tooltip, popover, …) is placed against.
/// Surfaced as <c>data-side</c>; names match the @floating-ui placements the positioning primitive uses.
/// </summary>
public enum Side
{
    /// <summary>Above the reference (the default).</summary>
    Top,

    /// <summary>To the inline-end of the reference.</summary>
    Right,

    /// <summary>Below the reference.</summary>
    Bottom,

    /// <summary>To the inline-start of the reference.</summary>
    Left,
}

/// <summary>
/// Alignment of a floating surface along its reference edge. Surfaced as <c>data-align</c>.
/// </summary>
public enum Align
{
    /// <summary>Aligned to the start of the reference edge.</summary>
    Start,

    /// <summary>Centred on the reference edge (the default).</summary>
    Center,

    /// <summary>Aligned to the end of the reference edge.</summary>
    End,
}

/// <summary>
/// Lower-cased string forms used in <c>data-*</c> / <c>aria-*</c> attributes, matching the
/// conventions the styled layer targets (e.g. <c>data-[orientation=vertical]</c>).
/// </summary>
public static class BzEnumExtensions
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

    /// <summary>The <c>data-side</c> / @floating-ui string for a placement side.</summary>
    public static string ToAttribute(this Side side) => side switch
    {
        Side.Right => "right",
        Side.Bottom => "bottom",
        Side.Left => "left",
        _ => "top",
    };

    /// <summary>The <c>data-align</c> / @floating-ui string for an alignment.</summary>
    public static string ToAttribute(this Align align) => align switch
    {
        Align.Start => "start",
        Align.End => "end",
        _ => "center",
    };
}
