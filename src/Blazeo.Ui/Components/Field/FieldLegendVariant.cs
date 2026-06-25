using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Type scale of a <see cref="BzFieldLegend"/>.</summary>
public enum FieldLegendVariant
{
    /// <summary>Legend-sized (text-base; the default).</summary>
    [Description("legend")]
    Legend,

    /// <summary>Label-sized (text-sm) - when the legend should read like a field label.</summary>
    [Description("label")]
    Label,
}
