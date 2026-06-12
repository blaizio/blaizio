using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Layout axis of a <see cref="Field"/>.</summary>
public enum FieldOrientation
{
    /// <summary>Label above the control (the default).</summary>
    [Description("vertical")]
    Vertical,

    /// <summary>Label and control on one row (e.g. a Switch row).</summary>
    [Description("horizontal")]
    Horizontal,

    /// <summary>Vertical, switching to horizontal when the enclosing <see cref="FieldGroup"/> is wide enough.</summary>
    [Description("responsive")]
    Responsive,
}
