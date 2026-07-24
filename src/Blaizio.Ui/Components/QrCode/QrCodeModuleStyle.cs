using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Shape of the data modules (the small "pixels") of a <see cref="BzQrCode"/>.</summary>
public enum QrCodeModuleStyle
{
    /// <summary>Classic solid squares. Crispest edges, safest to scan.</summary>
    [Description("square")]
    Square,

    /// <summary>Squares with rounded corners.</summary>
    [Description("rounded")]
    Rounded,

    /// <summary>Circular dots.</summary>
    [Description("dots")]
    Dots,

    /// <summary>Diamonds (squares rotated 45 degrees).</summary>
    [Description("diamond")]
    Diamond,
}
