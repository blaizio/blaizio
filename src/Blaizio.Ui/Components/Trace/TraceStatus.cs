using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>The state of the work a <see cref="BzTraceLine"/> reports.</summary>
public enum TraceStatus
{
    /// <summary>The work is done - "Ran dotnet test" (the default; no <c>data-status</c>).</summary>
    [Description("")]
    Done,

    /// <summary>In progress - "Running dotnet build". The title shimmers; write it in the present tense.</summary>
    [Description("running")]
    Running,

    /// <summary>Failed - "Failed to run dotnet test". The line turns destructive; say so in the title too.</summary>
    [Description("error")]
    Error,

    /// <summary>Not started yet, such as a plan step - "Run dotnet test". The line dims.</summary>
    [Description("pending")]
    Pending,
}
