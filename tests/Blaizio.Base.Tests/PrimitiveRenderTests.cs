using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>Render tests for the composition cornerstone: dynamic tag, attribute splat, RenderAs, cascade.</summary>
public class PrimitiveRenderTests : TestContext
{
    // Loose so primitives that import a JS module on render (e.g. roving focus) need no configured interop.
    public PrimitiveRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void BzPrimitive_renders_chosen_tag_and_splats_attributes()
    {
        var cut = RenderComponent<BasePrimitive>(p => p
            .Add(x => x.Element, "section")
            .Add(x => x.Attributes, new Dictionary<string, object> { ["id"] = "x", ["data-foo"] = "bar" })
            .AddChildContent("hi"));

        cut.MarkupMatches("<section id=\"x\" data-foo=\"bar\">hi</section>");
    }

    [Fact]
    public void BzPrimitive_RenderAs_hands_attributes_to_consumer_element()
    {
        var cut = RenderComponent<BasePrimitive>(p => p
            .Add(x => x.Attributes, new Dictionary<string, object> { ["data-state"] = "open" })
            .Add(x => x.RenderAs, (RenderFragment<BzRenderProps>)(props => builder =>
            {
                builder.OpenElement(0, "a");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddContent(2, "link");
                builder.CloseElement();
            })));

        // No wrapper element - behaviour is applied directly to the consumer's <a>.
        cut.MarkupMatches("<a data-state=\"open\">link</a>");
    }

    [Fact]
    public void BzSeparator_decorative_is_role_none_without_aria()
    {
        var cut = RenderComponent<BaseSeparator>();

        cut.MarkupMatches("<div role=\"none\" data-orientation=\"horizontal\"></div>");
    }

    [Fact]
    public void BzSeparator_semantic_vertical_sets_role_and_aria_orientation()
    {
        var cut = RenderComponent<BaseSeparator>(p => p
            .Add(x => x.Decorative, false)
            .Add(x => x.Orientation, Orientation.Vertical));

        cut.MarkupMatches("<div role=\"separator\" aria-orientation=\"vertical\" data-orientation=\"vertical\"></div>");
    }

    [Fact]
    public void BzSeparator_semantic_horizontal_omits_aria_orientation()
    {
        var cut = RenderComponent<BaseSeparator>(p => p.Add(x => x.Decorative, false));

        cut.MarkupMatches("<div role=\"separator\" data-orientation=\"horizontal\"></div>");
    }

    [Fact]
    public void BzSeparator_passes_through_consumer_class()
    {
        var cut = RenderComponent<BaseSeparator>(p => p.Add(x => x.Class, "h-px bg-border"));

        Assert.Equal("h-px bg-border", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void BzDirectionProvider_cascades_direction_to_descendants()
    {
        var cut = RenderComponent<BaseDirectionProvider>(p => p
            .Add(x => x.Direction, Direction.Rtl)
            .AddChildContent<DirectionProbe>());

        // The probe surfaces the resolved cascaded direction as the wrapper's text content.
        Assert.Equal("rtl", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void BzDirectionProvider_emits_dir_on_a_display_contents_wrapper()
    {
        // The DOM `dir` (on a layout-neutral wrapper) is what lets a subtree run a different
        // direction than <html> - CSS logical props + DOM-reading behaviour follow it.
        var cut = RenderComponent<BaseDirectionProvider>(p => p
            .Add(x => x.Direction, Direction.Rtl)
            .AddChildContent("x"));

        var wrapper = cut.Find("div");
        Assert.Equal("rtl", wrapper.GetAttribute("dir"));
        Assert.Contains("display:contents", wrapper.GetAttribute("style"));
    }

    [Fact]
    public void Dir_override_emits_dir_on_the_element_only_when_set_explicitly()
    {
        // No explicit Dir: no dir attribute, so the element inherits the ambient direction.
        var inherited = RenderComponent<BaseRovingFocusGroup>();
        Assert.False(inherited.Find("div").HasAttribute("dir"));

        // Explicit Dir: pinned on the container, so CSS + the roving getComputedStyle read follow it
        // (a real per-component override of the DirectionProvider, not just a C# value).
        var pinned = RenderComponent<BaseRovingFocusGroup>(p => p.Add(x => x.Dir, Direction.Rtl));
        Assert.Equal("rtl", pinned.Find("div").GetAttribute("dir"));
    }

    /// <summary>Minimal probe that surfaces the resolved cascaded direction as text.</summary>
    private sealed class DirectionProbe : BzComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, ResolvedDirection.ToAttribute());
    }
}
