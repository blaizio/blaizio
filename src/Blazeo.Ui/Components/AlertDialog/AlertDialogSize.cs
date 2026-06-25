using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Size of an <see cref="BzAlertDialogContent"/> - drives the max-width via <c>data-size</c>.</summary>
public enum AlertDialogSize
{
    /// <summary>Roomier on larger screens (sm:max-w-lg).</summary>
    [Description("default")]
    Default,

    /// <summary>Compact; a two-column footer on small screens.</summary>
    [Description("sm")]
    Sm,
}
