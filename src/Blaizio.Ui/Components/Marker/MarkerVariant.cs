using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Layout of a <see cref="BzMarker"/>.</summary>
public enum MarkerVariant
{
    /// <summary>An inline row for status, notes, and actions (the default).</summary>
    [Description("default")]
    Default,

    /// <summary>A row with a bottom border, for stacked activity lists.</summary>
    [Description("border")]
    Border,

    /// <summary>A centred label with divider lines on each side - date separators, section breaks.</summary>
    [Description("separator")]
    Separator,
}
