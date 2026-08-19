using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>The surface treatment of a <see cref="BzPanel"/>.</summary>
public enum PanelVariant
{
    /// <summary>Flat against the content, sharing one border with it - reads as part of the page.</summary>
    [Description("attached")] Attached,

    /// <summary>A detached card: inset from the edges with a full border, rounded corners and a shadow.</summary>
    [Description("floating")] Floating,
}
