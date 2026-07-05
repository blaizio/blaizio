namespace Blaizio;

/// <summary>
/// The tri-state check value of one tree node. Leaves are checked or unchecked directly; a
/// branch's state is derived from its descendant leaves (all → <see cref="Checked"/>, some →
/// <see cref="Indeterminate"/>, none → <see cref="Unchecked"/>).
/// </summary>
public enum TreeCheckState
{
    /// <summary>Not checked (for a branch: no descendant leaf is checked).</summary>
    Unchecked,

    /// <summary>Checked (for a branch: every descendant leaf is checked).</summary>
    Checked,

    /// <summary>A branch with some, but not all, descendant leaves checked.</summary>
    Indeterminate,
}
