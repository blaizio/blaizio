using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// Error-correction level of a <see cref="BzQrCode"/> - how much of the symbol can be damaged or
/// covered (e.g. by a center logo) and still scan.
/// </summary>
public enum QrCodeErrorCorrection
{
    /// <summary>Recovers ~7% of the symbol. Densest data, smallest code.</summary>
    [Description("low")]
    Low,

    /// <summary>Recovers ~15% of the symbol. The all-round default.</summary>
    [Description("medium")]
    Medium,

    /// <summary>Recovers ~25% of the symbol.</summary>
    [Description("quartile")]
    Quartile,

    /// <summary>Recovers ~30% of the symbol. Use with a center image or overlay.</summary>
    [Description("high")]
    High,
}
