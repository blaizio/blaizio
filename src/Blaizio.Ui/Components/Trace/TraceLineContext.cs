namespace Blaizio.Ui;

/// <summary>What a <see cref="BzTraceLine"/> shares with its parts: its status, which a <see cref="BzTraceStatusIcon"/> draws.</summary>
/// <param name="Status">The line's status.</param>
public sealed record TraceLineContext(TraceStatus Status);
