using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Rendered size of a <see cref="BzQrCode"/>. Override freely via <c>Class</c>.</summary>
public enum QrCodeSize
{
    /// <summary>96px square.</summary>
    [Description("sm")]
    Sm,

    /// <summary>128px square. The default.</summary>
    [Description("md")]
    Default,

    /// <summary>176px square.</summary>
    [Description("lg")]
    Lg,

    /// <summary>240px square.</summary>
    [Description("xl")]
    Xl,
}
