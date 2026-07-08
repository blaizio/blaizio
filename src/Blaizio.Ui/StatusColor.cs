using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>
/// Semantic color for form/feedback controls (Progress, Switch, Checkbox, Slider): the checked
/// fill / indicator is repainted with the matching status token. Flowed to the style sheet via
/// <c>data-color</c> (omitted for <see cref="Default"/>, which keeps the primary look).
/// </summary>
public enum StatusColor
{
    /// <summary>The primary accent (no <c>data-color</c> emitted).</summary>
    [Description("default")]
    Default,

    /// <summary>Positive - the <c>--success</c> token.</summary>
    [Description("success")]
    Success,

    /// <summary>Caution - the <c>--warning</c> token.</summary>
    [Description("warning")]
    Warning,

    /// <summary>Informational - the <c>--info</c> token.</summary>
    [Description("info")]
    Info,

    /// <summary>Error - the <c>--destructive</c> token.</summary>
    [Description("destructive")]
    Destructive,
}
