using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Inline alignment of a <see cref="BzMessage"/> - the sender's side of the thread.</summary>
public enum MessageAlign
{
    /// <summary>Aligned to the inline start - incoming messages (the default).</summary>
    [Description("start")]
    Start,

    /// <summary>Aligned to the inline end - the current user's messages.</summary>
    [Description("end")]
    End,
}
