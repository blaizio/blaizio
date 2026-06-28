using System.ComponentModel;

namespace Blazeo;

/// <summary>The axis a <see cref="BasePanelGroup"/> lays its panels out along (and resizes them on).</summary>
public enum ResizeDirection
{
    /// <summary>Panels sit side by side; handles resize their widths (the default).</summary>
    [Description("horizontal")] Horizontal,

    /// <summary>Panels stack; handles resize their heights.</summary>
    [Description("vertical")] Vertical,
}
