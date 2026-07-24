using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Shape of the three finder patterns (the "eyes") of a <see cref="BzQrCode"/>.</summary>
public enum QrCodeEyeStyle
{
    /// <summary>Classic square frames.</summary>
    [Description("square")]
    Square,

    /// <summary>Square frames with rounded corners.</summary>
    [Description("rounded")]
    Rounded,

    /// <summary>Concentric circles.</summary>
    [Description("circle")]
    Circle,
}
