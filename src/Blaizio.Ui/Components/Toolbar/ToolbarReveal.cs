using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// What a toolbar control does when it would sit on a clipped row of an
/// <see cref="ToolbarOverflow.Expand"/> bar - for controls that appear contextually and must not
/// go unseen behind the toggle. Inert under the other overflow modes, except that
/// <see cref="Pin"/> still orders the control first under <see cref="ToolbarOverflow.Wrap"/>.
/// </summary>
public enum ToolbarReveal
{
    /// <summary>Normal flow: the control clips like any other (the default).</summary>
    [Description("none")] None,

    /// <summary>
    /// Order the control first, so it lands on the visible row and pushes an older control down
    /// instead. The bar stays one row; deterministic, no motion.
    /// </summary>
    [Description("pin")] Pin,

    /// <summary>
    /// Hold the bar open while the control sits on a clipped row: the bar expands when the
    /// control appears below the clip and settles back when it leaves. Document flow, so the
    /// content below the bar moves.
    /// </summary>
    [Description("expand")] Expand,
}
