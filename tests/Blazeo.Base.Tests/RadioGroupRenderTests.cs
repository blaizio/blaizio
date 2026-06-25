using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + selection tests for the headless radio group. The roving-focus keyboard nav is JS
/// (verified in-browser); these cover the C# contract: roles, ARIA, the roving markers, and that
/// clicking selects exactly one item. JSInterop is Loose so the roving module import is a no-op.
/// </summary>
public class RadioGroupRenderTests : TestContext
{
    public RadioGroupRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Items() => builder =>
    {
        var seq = 0;
        foreach (var value in new[] { "a", "b", "c" })
        {
            builder.OpenComponent<BaseRadioGroupItem>(seq++);
            builder.AddComponentParameter(seq++, nameof(BaseRadioGroupItem.Value), value);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Group_is_a_radiogroup_of_radios_with_roving_markers_and_no_tabindex()
    {
        var cut = RenderComponent<BaseRadioGroup>(p => p.AddChildContent(Items()));

        Assert.Equal("radiogroup", cut.Find("[role=radiogroup]").GetAttribute("role"));

        var radios = cut.FindAll("[role=radio]");
        Assert.Equal(3, radios.Count);
        foreach (var radio in radios)
        {
            Assert.Equal("button", radio.GetAttribute("type"));
            Assert.True(radio.HasAttribute("data-bz-roving-item"));
            Assert.Equal("false", radio.GetAttribute("aria-checked"));
            Assert.Equal("unchecked", radio.GetAttribute("data-state"));
            Assert.False(radio.HasAttribute("tabindex")); // owned by the roving-focus script
        }
    }

    [Fact]
    public void DefaultValue_checks_that_item_and_marks_it_the_active_tab_stop()
    {
        var cut = RenderComponent<BaseRadioGroup>(p => p
            .Add(x => x.DefaultValue, "b")
            .AddChildContent(Items()));

        var radios = cut.FindAll("[role=radio]");
        Assert.Equal("checked", radios[1].GetAttribute("data-state"));
        Assert.Equal("true", radios[1].GetAttribute("aria-checked"));
        Assert.True(radios[1].HasAttribute("data-roving-active"));
        Assert.Equal("unchecked", radios[0].GetAttribute("data-state"));
        Assert.False(radios[0].HasAttribute("data-roving-active"));
    }

    [Fact]
    public void Clicking_an_item_selects_exactly_one()
    {
        var cut = RenderComponent<BaseRadioGroup>(p => p.AddChildContent(Items()));

        cut.FindAll("[role=radio]")[2].Click();

        var radios = cut.FindAll("[role=radio]");
        Assert.Equal("checked", radios[2].GetAttribute("data-state"));
        Assert.Equal("unchecked", radios[0].GetAttribute("data-state"));
        Assert.Equal("unchecked", radios[1].GetAttribute("data-state"));
    }

    [Fact]
    public void Selecting_a_second_item_moves_the_selection()
    {
        var cut = RenderComponent<BaseRadioGroup>(p => p
            .Add(x => x.DefaultValue, "a")
            .AddChildContent(Items()));

        cut.FindAll("[role=radio]")[1].Click();

        var radios = cut.FindAll("[role=radio]");
        Assert.Equal("unchecked", radios[0].GetAttribute("data-state"));
        Assert.Equal("checked", radios[1].GetAttribute("data-state"));
    }

    [Fact]
    public void Disabled_group_disables_every_item_and_blocks_selection()
    {
        var cut = RenderComponent<BaseRadioGroup>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Items()));

        Assert.True(cut.Find("[role=radiogroup]").HasAttribute("data-disabled"));
        foreach (var radio in cut.FindAll("[role=radio]"))
            Assert.True(radio.HasAttribute("disabled"));

        cut.FindAll("[role=radio]")[0].Click();
        Assert.Equal("unchecked", cut.FindAll("[role=radio]")[0].GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_group_does_not_self_update_but_raises_change()
    {
        string? changed = null;
        var cut = RenderComponent<BaseRadioGroup>(p => p
            .Add(x => x.Value, "a")
            .Add(x => x.ValueChanged, (string v) => changed = v)
            .AddChildContent(Items()));

        cut.FindAll("[role=radio]")[2].Click();

        // Controlled: stays on "a" until the parent flows a new Value back…
        Assert.Equal("checked", cut.FindAll("[role=radio]")[0].GetAttribute("data-state"));
        // …but the intended value was announced.
        Assert.Equal("c", changed);
    }
}
