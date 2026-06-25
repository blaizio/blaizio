namespace Blazeo;

/// <summary>
/// Shared state a <see cref="BaseTooltipProvider"/> cascades to the tooltips inside it, implementing
/// the "skip delay" grouping: once one tooltip has opened, moving the pointer to another trigger in
/// the group opens it immediately (no hover delay) for a short window after the last one closes.
/// </summary>
/// <remarks>
/// A plain mutable holder, not a render-affecting value - tooltips read <see cref="ShouldSkipDelay"/>
/// at the moment of hover, so no re-render is needed when the skip window opens or closes.
/// </remarks>
public sealed class TooltipProviderContext(int delayDuration, int skipDelayDuration) : IDisposable
{
    private int _openCount;
    private bool _inSkipWindow;
    private CancellationTokenSource? _skipCts;

    /// <summary>The group's default hover delay in milliseconds. A tooltip's own DelayDuration overrides it.</summary>
    public int DelayDuration { get; } = delayDuration;

    /// <summary>
    /// True while a tooltip in the group is open, or within <c>skipDelayDuration</c> ms after the
    /// last one closed - the windows in which the next tooltip should open without its hover delay.
    /// </summary>
    public bool ShouldSkipDelay => _openCount > 0 || _inSkipWindow;

    /// <summary>A grouped tooltip reports it opened.</summary>
    public void NotifyOpen()
    {
        _openCount++;
        _inSkipWindow = true;
        _skipCts?.Cancel();
        _skipCts = null;
    }

    /// <summary>A grouped tooltip reports it closed; once the last closes, the skip window starts ticking down.</summary>
    public void NotifyClose()
    {
        if (_openCount > 0) _openCount--;
        if (_openCount > 0) return;

        _skipCts?.Cancel();
        var cts = _skipCts = new CancellationTokenSource();
        _ = CloseSkipWindowAsync(cts);
    }

    private async Task CloseSkipWindowAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(skipDelayDuration, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _inSkipWindow = false;
    }

    public void Dispose()
    {
        _skipCts?.Cancel();
        _skipCts?.Dispose();
    }
}
