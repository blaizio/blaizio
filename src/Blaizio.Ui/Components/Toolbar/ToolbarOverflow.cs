using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// How a horizontal <see cref="BzToolbar"/> behaves when its controls outgrow the width available
/// to it. Only matters once something constrains the bar (a <c>w-full</c>/<c>max-w-*</c> in
/// <c>Class</c>, or a narrow parent) - an unconstrained bar simply grows. Vertical bars ignore it.
/// </summary>
public enum ToolbarOverflow
{
    /// <summary>Controls wrap onto further rows (the default).</summary>
    [Description("wrap")] Wrap,

    /// <summary>
    /// One row that scrolls, with chevron buttons paging it (see
    /// <see cref="BzToolbar.ScrollButtons"/> for their placement). The chevrons show only while
    /// the row overflows. Arrow-key travel scrolls the focused control into view on its own - the
    /// chevrons are a pointer affordance.
    /// </summary>
    [Description("scroll")] Scroll,

    /// <summary>
    /// One clipped row plus a trailing toggle that expands the bar to show the wrapped rest.
    /// The toggle shows only while the controls wrap onto a second row. Arrowing into a clipped
    /// control expands the bar for as long as focus stays inside.
    /// </summary>
    [Description("expand")] Expand,
}

/// <summary>Where <see cref="ToolbarOverflow.Scroll"/> places its chevron buttons.</summary>
public enum ToolbarScrollButtons
{
    /// <summary>Prev at the bar's start edge, next at its end edge (the default).</summary>
    [Description("split")] Split,

    /// <summary>Both chevrons together at the end edge.</summary>
    [Description("end")] End,
}
