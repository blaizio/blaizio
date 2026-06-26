namespace Blazeo.Docs;

/// <summary>
/// One node of a <see cref="CompositionTree"/>: a component in the anatomy plus its nested children.
/// Build a tree with the params constructor:
/// <code>
/// new CompositionNode("DropdownMenu",
///     new CompositionNode("DropdownMenuTrigger"),
///     new CompositionNode("DropdownMenuContent",
///         new CompositionNode("DropdownMenuItem")));
/// </code>
/// </summary>
/// <param name="Name">The component name shown on the row (e.g. <c>DropdownMenuTrigger</c>).</param>
/// <param name="Children">Nested components, drawn indented beneath this one.</param>
public sealed record CompositionNode(string Name, IReadOnlyList<CompositionNode> Children)
{
    /// <summary>Convenience: a node with zero or more children passed inline.</summary>
    public CompositionNode(string name, params CompositionNode[] children)
        : this(name, (IReadOnlyList<CompositionNode>)children)
    {
    }
}
