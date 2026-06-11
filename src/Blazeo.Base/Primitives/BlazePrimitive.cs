using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazeo;

/// <summary>
/// The low-level renderer every Blazeo primitive ultimately delegates to. It renders a single
/// element whose tag is chosen at runtime (<see cref="As"/>), splats one pre-merged attribute
/// dictionary (<see cref="Attributes"/>) onto it, and optionally captures the element reference.
/// </summary>
/// <remarks>
/// <b>asChild:</b> when an <see cref="AsChild"/> fragment is supplied, <see cref="BlazePrimitive"/>
/// renders <i>nothing of its own</i> and instead invokes that fragment with the merged
/// <see cref="BlazeRenderProps"/>, letting the consumer render their own element/component and splat
/// the behaviour onto it. This is the faithful Blazor analogue of Radix's <c>asChild</c>: behaviour
/// without an imposed wrapper element.
/// </remarks>
public sealed class BlazePrimitive : ComponentBase
{
    /// <summary>The HTML tag to render when not in <see cref="AsChild"/> mode. Defaults to <c>div</c>.</summary>
    [Parameter] public string As { get; set; } = "div";

    /// <summary>The merged attribute dictionary to splat onto the element (typically from <see cref="PropBuilder.Merge"/>).</summary>
    [Parameter] public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    /// <summary>Inner content of the rendered element.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// When supplied, takes over rendering: the consumer's fragment receives the merged
    /// <see cref="BlazeRenderProps"/> and is responsible for rendering an element and splatting
    /// <c>props.Attributes</c> onto it. <see cref="As"/> and <see cref="ChildContent"/> are ignored.
    /// </summary>
    [Parameter] public RenderFragment<BlazeRenderProps>? AsChild { get; set; }

    /// <summary>Optional hook to capture the rendered element's <see cref="ElementReference"/> (ignored in asChild mode).</summary>
    [Parameter] public Action<ElementReference>? ElementRefCaptured { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var attributes = Attributes ?? BlazeRenderProps.EmptyAttributes;

        if (AsChild is not null)
        {
            builder.AddContent(0, AsChild(new BlazeRenderProps(attributes)));
            return;
        }

        builder.OpenElement(1, As);
        builder.AddMultipleAttributes(2, attributes);
        if (ElementRefCaptured is not null)
        {
            builder.AddElementReferenceCapture(3, reference => ElementRefCaptured(reference));
        }
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
