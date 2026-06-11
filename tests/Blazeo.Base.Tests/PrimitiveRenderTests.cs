using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>Render tests for the composition cornerstone: dynamic tag, attribute splat, asChild, cascade.</summary>
public class PrimitiveRenderTests : TestContext
{
    [Fact]
    public void BlazePrimitive_renders_chosen_tag_and_splats_attributes()
    {
        var cut = RenderComponent<BlazePrimitive>(p => p
            .Add(x => x.As, "section")
            .Add(x => x.Attributes, new Dictionary<string, object> { ["id"] = "x", ["data-foo"] = "bar" })
            .AddChildContent("hi"));

        cut.MarkupMatches("<section id=\"x\" data-foo=\"bar\">hi</section>");
    }

    [Fact]
    public void BlazePrimitive_asChild_hands_attributes_to_consumer_element()
    {
        var cut = RenderComponent<BlazePrimitive>(p => p
            .Add(x => x.Attributes, new Dictionary<string, object> { ["data-state"] = "open" })
            .Add(x => x.AsChild, (RenderFragment<BlazeRenderProps>)(props => builder =>
            {
                builder.OpenElement(0, "a");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddContent(2, "link");
                builder.CloseElement();
            })));

        // No wrapper element — behaviour is applied directly to the consumer's <a>.
        cut.MarkupMatches("<a data-state=\"open\">link</a>");
    }

    [Fact]
    public void BlazeSeparator_decorative_is_role_none_without_aria()
    {
        var cut = RenderComponent<BlazeSeparator>();

        cut.MarkupMatches("<div role=\"none\" data-orientation=\"horizontal\"></div>");
    }

    [Fact]
    public void BlazeSeparator_semantic_vertical_sets_role_and_aria_orientation()
    {
        var cut = RenderComponent<BlazeSeparator>(p => p
            .Add(x => x.Decorative, false)
            .Add(x => x.Orientation, Orientation.Vertical));

        cut.MarkupMatches("<div role=\"separator\" aria-orientation=\"vertical\" data-orientation=\"vertical\"></div>");
    }

    [Fact]
    public void BlazeSeparator_semantic_horizontal_omits_aria_orientation()
    {
        var cut = RenderComponent<BlazeSeparator>(p => p.Add(x => x.Decorative, false));

        cut.MarkupMatches("<div role=\"separator\" data-orientation=\"horizontal\"></div>");
    }

    [Fact]
    public void BlazeSeparator_passes_through_consumer_class()
    {
        var cut = RenderComponent<BlazeSeparator>(p => p.Add(x => x.Class, "h-px bg-border"));

        Assert.Equal("h-px bg-border", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void BlazeDirectionProvider_cascades_direction_to_descendants()
    {
        var cut = RenderComponent<BlazeDirectionProvider>(p => p
            .Add(x => x.Direction, Direction.Rtl)
            .AddChildContent<DirectionProbe>());

        Assert.Equal("rtl", cut.Markup.Trim());
    }

    /// <summary>Minimal probe that surfaces the resolved cascaded direction as text.</summary>
    private sealed class DirectionProbe : BlazeComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, ResolvedDirection.ToAttribute());
    }
}
