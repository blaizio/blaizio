using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// RenderAs props are splatted onto whatever the consumer renders - an element OR a component. On a
/// component Blazor matches keys to parameters case-insensitively, so an element-only attribute like
/// <c>disabled="disabled"</c> is assigned to a <c>bool Disabled</c> parameter and throws. Primitives
/// must therefore emit native element attributes only when they render the element themselves, and
/// convey the state through <c>aria-*</c>/<c>data-*</c> otherwise.
/// </summary>
public class RenderAsSplatTests : BunitContext
{
    public RenderAsSplatTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    /// <summary>Stands in for the styled layer: the parameter shapes a props dictionary can collide with.</summary>
    private sealed class StyledProbe : ComponentBase
    {
        [Parameter] public bool Disabled { get; set; }
        [Parameter] public bool ReadOnly { get; set; }
        [Parameter] public string? Class { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, Attributes);
            builder.CloseElement();
        }
    }

    private static RenderFragment<BzRenderProps> OntoStyledProbe() => props => builder =>
    {
        builder.OpenComponent<StyledProbe>(0);
        builder.AddMultipleAttributes(1, props.Attributes);
        builder.CloseComponent();
    };

    private static void AssertConveysDisabledWithoutNativeAttribute(IRenderedComponent<BaseCombobox> cut)
    {
        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void Disabled_combobox_trigger_renders_onto_a_component()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BaseComboboxTrigger>(t => t.Add(x => x.RenderAs, OntoStyledProbe())));

        AssertConveysDisabledWithoutNativeAttribute(cut);
    }

    [Fact]
    public void Disabled_combobox_clear_renders_onto_a_component()
    {
        // Clear renders only while something is selected - there is nothing to clear otherwise.
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.DefaultValue, "a")
            .AddChildContent<BaseComboboxClear>(t => t.Add(x => x.RenderAs, OntoStyledProbe())));

        AssertConveysDisabledWithoutNativeAttribute(cut);
    }

    [Fact]
    public void Disabled_combobox_chip_remove_renders_onto_a_component()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BaseComboboxChipRemove>(t => t.Add(x => x.RenderAs, OntoStyledProbe())));

        AssertConveysDisabledWithoutNativeAttribute(cut);
    }

    [Fact]
    public void Disabled_input_tags_chip_remove_renders_onto_a_component()
    {
        var cut = Render<BaseInputTags>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BaseInputTagsChipRemove>(t => t.Add(x => x.RenderAs, OntoStyledProbe())));

        var button = cut.Find("button[data-bz-input-tags-chip-remove]");
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("disabled"));
    }

    // The element path must keep the real attribute - aria-disabled alone would leave it clickable.
    [Fact]
    public void Disabled_combobox_trigger_still_emits_native_disabled_on_its_own_button()
    {
        var cut = Render<BaseCombobox>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<BaseComboboxTrigger>());

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }
}
