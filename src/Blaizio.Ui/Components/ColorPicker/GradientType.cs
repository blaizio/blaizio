using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>The shape a <see cref="GradientValue"/> paints its stops along.</summary>
public enum GradientType
{
    /// <summary>A straight ramp along <see cref="GradientValue.Angle"/> - CSS <c>linear-gradient</c>. The default.</summary>
    [Description("Linear")]
    Linear,

    /// <summary>Rings out from the centre - CSS <c>radial-gradient</c>. Ignores the angle.</summary>
    [Description("Radial")]
    Radial,

    /// <summary>Sweeps around the centre from <see cref="GradientValue.Angle"/> - CSS <c>conic-gradient</c>.</summary>
    [Description("Angular")]
    Angular,

    /// <summary>Square rings out from the centre. CSS has no diamond function, so it renders as four quadrant ramps composited into one <c>background-image</c> value. Ignores the angle.</summary>
    [Description("Diamond")]
    Diamond,
}
