using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of a <see cref="BzTabsList"/>.</summary>
public enum TabsListVariant
{
    /// <summary>Filled pill strip - the active tab gets a raised surface.</summary>
    [Description("default")]
    Default,

    /// <summary>Transparent strip - the active tab gets an underline.</summary>
    [Description("line")]
    Line,
}
