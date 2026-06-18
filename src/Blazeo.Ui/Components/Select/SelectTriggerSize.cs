using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>The height of a <see cref="SelectTrigger"/>, emitted as its <c>data-size</c> attribute.</summary>
public enum SelectTriggerSize
{
    /// <summary>The standard trigger height.</summary>
    [Description("default")]
    Default,

    /// <summary>A more compact trigger, for dense layouts.</summary>
    [Description("sm")]
    Sm,
}
