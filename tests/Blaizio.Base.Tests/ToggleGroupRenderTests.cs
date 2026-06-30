using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + selection tests for the headless toggle group. The roving-focus keyboard nav is JS
/// (verified in-browser); these cover the C# contract: the per-mode ARIA, the roving markers, and
/// the single/multiple toggling rules. JSInterop is Loose so the roving module import is a no-op.
/// </summary>
public class ToggleGroupRenderTests : TestContext
{
    public ToggleGroupRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment Items() => builder =>
    {
        var seq = 0;
        foreach (var value in new[] { "a", "b", "c" })
        {
            builder.OpenComponent<BaseToggleGroupItem>(seq++);
            builder.AddComponentParameter(seq++, nameof(BaseToggleGroupItem.Value), value);
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Single_group_is_a_group_of_uncheckable_radios_with_roving_markers_and_no_tabindex()
    {
        var cut = RenderComponent<BaseToggleGroup>(p => p.AddChildContent(Items()));

        Assert.Equal("group", cut.Find("[role=group]").GetAttribute("role"));

        var items = cut.FindAll("[role=radio]");
        Assert.Equal(3, items.Count);
        foreach (var item in items)
        {
            Assert.Equal("button", item.GetAttribute("type"));
            Assert.True(item.HasAttribute("data-bz-roving-item"));
            Assert.Equal("false", item.GetAttribute("aria-checked"));
            Assert.False(item.HasAttribute("aria-pressed"));
            Assert.Equal("off", item.GetAttribute("data-state"));
            Assert.False(item.HasAttribute("tabindex")); // owned by the roving-focus script
        }
    }

    [Fact]
    public void Multiple_group_items_are_toggle_buttons_not_radios()
    {
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Type, ToggleGroupType.Multiple)
            .AddChildContent(Items()));

        Assert.Empty(cut.FindAll("[role=radio]"));

        var items = cut.FindAll("[data-bz-roving-item]");
        Assert.Equal(3, items.Count);
        foreach (var item in items)
            Assert.Equal("false", item.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void DefaultValue_turns_that_item_on_and_marks_it_the_active_tab_stop()
    {
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.DefaultValue, "b")
            .AddChildContent(Items()));

        var items = cut.FindAll("[role=radio]");
        Assert.Equal("on", items[1].GetAttribute("data-state"));
        Assert.Equal("true", items[1].GetAttribute("aria-checked"));
        Assert.True(items[1].HasAttribute("data-roving-active"));
        Assert.Equal("off", items[0].GetAttribute("data-state"));
        Assert.False(items[0].HasAttribute("data-roving-active"));
    }

    [Fact]
    public void Single_mode_keeps_one_on_and_clicking_it_again_turns_it_off()
    {
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.DefaultValue, "a")
            .AddChildContent(Items()));

        cut.FindAll("[role=radio]")[1].Click();
        var items = cut.FindAll("[role=radio]");
        Assert.Equal("off", items[0].GetAttribute("data-state"));
        Assert.Equal("on", items[1].GetAttribute("data-state"));

        cut.FindAll("[role=radio]")[1].Click();
        foreach (var item in cut.FindAll("[role=radio]"))
            Assert.Equal("off", item.GetAttribute("data-state"));
    }

    [Fact]
    public void Multiple_mode_accumulates_and_removes_values()
    {
        // Uncontrolled (no ValuesChanged bound) - the group keeps its own list.
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Type, ToggleGroupType.Multiple)
            .Add(x => x.DefaultValues, (IReadOnlyList<string>)["a"])
            .AddChildContent(Items()));

        cut.FindAll("[data-bz-roving-item]")[2].Click();
        var items = cut.FindAll("[data-bz-roving-item]");
        Assert.Equal("on", items[0].GetAttribute("data-state"));
        Assert.Equal("on", items[2].GetAttribute("data-state"));

        cut.FindAll("[data-bz-roving-item]")[0].Click();
        items = cut.FindAll("[data-bz-roving-item]");
        Assert.Equal("off", items[0].GetAttribute("data-state"));
        Assert.Equal("on", items[2].GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_multiple_group_announces_the_grown_list()
    {
        IReadOnlyList<string>? last = null;
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Type, ToggleGroupType.Multiple)
            .Add(x => x.Values, (IReadOnlyList<string>)["a"])
            .Add(x => x.ValuesChanged, (IReadOnlyList<string> v) => last = v)
            .AddChildContent(Items()));

        cut.FindAll("[data-bz-roving-item]")[2].Click();

        Assert.Equal(["a", "c"], last);
    }

    [Fact]
    public void Single_mode_deselect_reports_null()
    {
        string? last = "unset";
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Value, "a")
            .Add(x => x.ValueChanged, (string? v) => last = v)
            .AddChildContent(Items()));

        cut.FindAll("[role=radio]")[0].Click();

        Assert.Null(last);
    }

    [Fact]
    public void Disabled_group_disables_every_item_and_blocks_toggling()
    {
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(Items()));

        Assert.True(cut.Find("[role=group]").HasAttribute("data-disabled"));
        foreach (var item in cut.FindAll("[role=radio]"))
            Assert.True(item.HasAttribute("disabled"));

        cut.FindAll("[role=radio]")[0].Click();
        Assert.Equal("off", cut.FindAll("[role=radio]")[0].GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_group_does_not_self_update_but_raises_change()
    {
        string? changed = null;
        var cut = RenderComponent<BaseToggleGroup>(p => p
            .Add(x => x.Value, "a")
            .Add(x => x.ValueChanged, (string? v) => changed = v)
            .AddChildContent(Items()));

        cut.FindAll("[role=radio]")[2].Click();

        // Controlled: stays on "a" until the parent flows a new Value back…
        Assert.Equal("on", cut.FindAll("[role=radio]")[0].GetAttribute("data-state"));
        // …but the intended value was announced.
        Assert.Equal("c", changed);
    }
}
