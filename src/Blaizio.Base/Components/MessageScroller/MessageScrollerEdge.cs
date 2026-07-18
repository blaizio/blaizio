namespace Blaizio;

/// <summary>An edge of a message-scroller transcript.</summary>
public enum MessageScrollerEdge
{
    /// <summary>The live end of the transcript - the newest message (the default).</summary>
    End,

    /// <summary>The top of the transcript - the oldest loaded message.</summary>
    Start,
}
