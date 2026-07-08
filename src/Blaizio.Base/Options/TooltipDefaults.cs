namespace Blaizio;

/// <summary>App-wide tooltip timing defaults.</summary>
public sealed class TooltipDefaults
{
    /// <summary>Hover delay (ms) before a tooltip opens. Defaults to 400.</summary>
    public int DelayDuration { get; set; } = 400;

    /// <summary>How long (ms) after a tooltip closes that the next one in a group still opens instantly. Defaults to 300.</summary>
    public int SkipDelayDuration { get; set; } = 300;
}
