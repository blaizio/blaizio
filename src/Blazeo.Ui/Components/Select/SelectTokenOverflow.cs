namespace Blazeo.Ui;

/// <summary>How a multi-select trigger handles tokens that don't fit on one line.</summary>
public enum SelectTokenOverflow
{
    /// <summary>
    /// Keep the trigger one line tall: hide the tokens that overflow and show a trailing "+N" counter
    /// (before the chevron). The trigger's height never changes as the selection grows.
    /// </summary>
    Count,

    /// <summary>Let the tokens wrap onto more lines, growing the trigger's height to fit them all.</summary>
    Wrap,
}
