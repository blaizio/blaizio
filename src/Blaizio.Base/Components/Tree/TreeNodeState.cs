namespace Blaizio;

/// <summary>
/// A snapshot of one tree node's rendered state, handed to the node/indicator/checkbox templates
/// so custom content can react to everything the default rendering reacts to.
/// </summary>
/// <typeparam name="TItem">The consumer's node type.</typeparam>
/// <param name="Item">The consumer's data item for this node.</param>
/// <param name="Value">The node's unique value.</param>
/// <param name="Text">The node's display text (from the TextSelector).</param>
/// <param name="Depth">Zero-based depth (root nodes are 0).</param>
/// <param name="IsBranch">True when the node can hold children (expandable).</param>
/// <param name="Expanded">True when the branch is expanded.</param>
/// <param name="Selected">True when the node is selected.</param>
/// <param name="Focused">True when the node holds the tree's roving focus.</param>
/// <param name="Disabled">True when the node is disabled.</param>
/// <param name="Loading">True while the branch's children are being lazily loaded.</param>
/// <param name="Checked">The node's tri-state check value (meaningful when the tree is Checkable).</param>
/// <param name="PosInSet">One-based position among its siblings (aria-posinset).</param>
/// <param name="SetSize">Number of siblings at this level (aria-setsize).</param>
/// <param name="Renaming">True while the node is being renamed.</param>
/// <param name="Matched">True when the node is a direct hit for the active search term.</param>
public sealed record TreeNodeState<TItem>(
    TItem Item,
    string Value,
    string Text,
    int Depth,
    bool IsBranch,
    bool Expanded,
    bool Selected,
    bool Focused,
    bool Disabled,
    bool Loading,
    CheckedState Checked,
    int PosInSet,
    int SetSize,
    bool Renaming,
    bool Matched = false);

/// <summary>A committed rename: the node, its value, and the text the user entered.</summary>
/// <typeparam name="TItem">The consumer's node type.</typeparam>
/// <param name="Item">The renamed node's data item.</param>
/// <param name="Value">The renamed node's value.</param>
/// <param name="Text">The new text the user committed.</param>
public sealed record TreeRename<TItem>(TItem Item, string Value, string Text);
