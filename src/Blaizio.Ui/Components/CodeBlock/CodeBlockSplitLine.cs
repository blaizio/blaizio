using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blaizio.Ui;

/// <summary>How a <see cref="CodeBlockSplitLine"/> draws its text.</summary>
internal enum CodeBlockLineFormat
{
    Plain,
    Ansi,
    Syntax,
}

/// <summary>
/// One line that <see cref="BzCodeBlockContent"/> split its <c>Code</c> string into. Immutable: an
/// unchanged line keeps its instance from one render to the next, which is how
/// <see cref="CodeBlockSplitLineView"/> knows it has nothing to draw.
/// </summary>
/// <param name="text">The line's text, after any diff marker.</param>
/// <param name="variant">The line's variant.</param>
/// <param name="format">How the text is drawn.</param>
/// <param name="source">The tokenized line it was drawn from, for <see cref="CodeBlockLineFormat.Syntax"/>.</param>
/// <param name="content">What renders inside the line; <see langword="null"/> for an empty line.</param>
internal sealed class CodeBlockSplitLine(
    string text, CodeBlockLineVariant variant, CodeBlockLineFormat format, SyntaxLine? source, RenderFragment? content)
{
    public string Text { get; } = text;

    public CodeBlockLineVariant Variant { get; } = variant;

    public CodeBlockLineFormat Format { get; } = format;

    public SyntaxLine? Source { get; } = source;

    public RenderFragment? Content { get; } = content;
}

/// <summary>
/// Renders one <see cref="CodeBlockSplitLine"/> as a <see cref="BzCodeBlockLine"/>, and renders it
/// again only when it is handed a different line - so output streaming into a <c>Code</c> string
/// re-renders the new and changed lines, not every line already on screen.
/// </summary>
internal sealed class CodeBlockSplitLineView : ComponentBase
{
    private CodeBlockSplitLine? _rendered;

    /// <summary>The line to render.</summary>
    [Parameter, EditorRequired] public CodeBlockSplitLine Line { get; set; } = default!;

    protected override bool ShouldRender() => !ReferenceEquals(_rendered, Line);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _rendered = Line;
        builder.OpenComponent<BzCodeBlockLine>(0);
        builder.AddComponentParameter(1, nameof(BzCodeBlockLine.Variant), Line.Variant);
        // Always passed, null for an empty line: a parameter left out keeps its previous value, and
        // a line that turned empty would go on showing its old text.
        builder.AddComponentParameter(2, nameof(BzCodeBlockLine.ChildContent), Line.Content);
        builder.CloseComponent();
    }
}
