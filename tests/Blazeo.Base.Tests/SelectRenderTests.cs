using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>
/// Render + interaction tests for the headless select. Positioning (ts/positioning.ts), the presence
/// animation (ts/presence.ts), keyboard + pointer navigation (ts/menu.ts, including opening on the
/// selected option), and outside-pointer-down dismissal (ts/dismissableLayer.ts) are JS, verified
/// in-browser; these cover the C# contract: the trigger's combobox aria + anchor hook,
/// click-to-toggle, the always-mounted listbox (options register even while hidden, so a value shows
/// in the trigger before the listbox first opens), placeholder vs selected display, option selection
/// reporting + closing, disabled options, aria-selected, and controlled binding. JSInterop is Loose
/// so module imports are no-ops.
/// </summary>
public class SelectRenderTests : TestContext
{
    public SelectRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static void AddTrigger(RenderTreeBuilder b, int seq)
    {
        b.OpenComponent<BzSelectTrigger>(seq);
        b.AddComponentParameter(seq + 1, nameof(BzSelectTrigger.ChildContent), (RenderFragment)(t =>
        {
            t.OpenComponent<BzSelectValue>(0);
            t.AddComponentParameter(1, nameof(BzSelectValue.Placeholder), (RenderFragment)(p => p.AddContent(0, "Select")));
            t.CloseComponent();
        }));
        b.CloseComponent();
    }

    private static RenderFragment Body(RenderFragment items) => b =>
    {
        AddTrigger(b, 0);
        b.OpenComponent<BzSelectContent>(2);
        b.AddComponentParameter(3, nameof(BzSelectContent.ChildContent), items);
        b.CloseComponent();
    };

    private static RenderFragment Item(string value, string text, bool disabled = false) => b =>
    {
        b.OpenComponent<BzSelectItem>(0);
        b.AddComponentParameter(1, nameof(BzSelectItem.Value), value);
        b.AddComponentParameter(2, nameof(BzSelectItem.ChildContent), (RenderFragment)(x => x.AddContent(0, text)));
        if (disabled) b.AddComponentParameter(3, nameof(BzSelectItem.Disabled), true);
        b.CloseComponent();
    };

    private static RenderFragment Items(params RenderFragment[] items) => b =>
    {
        var i = 0;
        foreach (var item in items)
        {
            b.OpenRegion(i++);
            item(b);
            b.CloseRegion();
        }
    };

    [Fact]
    public void Closed_by_default_with_combobox_aria_and_hidden_listbox()
    {
        var cut = RenderComponent<BzSelect>(p => p.AddChildContent(Body(Item("apple", "Apple"))));

        var trigger = cut.Find("button");
        Assert.Equal("combobox", trigger.GetAttribute("role"));
        Assert.Equal("listbox", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("closed", trigger.GetAttribute("data-state"));
        Assert.True(trigger.HasAttribute("data-bz-select-anchor"));
        // Nothing selected -> the placeholder hook is present.
        Assert.True(trigger.HasAttribute("data-placeholder"));

        // The listbox stays in the DOM while closed (so options register), but hidden and data-state=closed.
        var listbox = cut.Find("[role=listbox]");
        Assert.True(listbox.HasAttribute("hidden"));
        Assert.Equal("closed", listbox.GetAttribute("data-state"));
        // The option is mounted even though the listbox is closed.
        Assert.Single(cut.FindAll("[role=option]"));
    }

    [Fact]
    public void Placeholder_shows_when_nothing_is_selected()
    {
        var cut = RenderComponent<BzSelect>(p => p.AddChildContent(Body(Item("apple", "Apple"))));

        Assert.Contains("Select", cut.Find("button").TextContent);
    }

    [Fact]
    public void Preselected_value_shows_in_the_trigger_before_opening()
    {
        // DefaultValue with no ValueChanged = uncontrolled. The "banana" option registers its content
        // (it is mounted in the hidden listbox), so the trigger shows it without ever opening.
        var cut = RenderComponent<BzSelect>(p => p
            .Add(x => x.DefaultValue, "banana")
            .AddChildContent(Body(Items(Item("apple", "Apple"), Item("banana", "Banana")))));

        var trigger = cut.Find("button");
        Assert.Contains("Banana", trigger.TextContent);
        Assert.False(trigger.HasAttribute("data-placeholder"));
    }

    [Fact]
    public void Click_opens_and_wires_the_trigger_to_the_listbox()
    {
        var cut = RenderComponent<BzSelect>(p => p.AddChildContent(Body(Item("apple", "Apple"))));

        cut.Find("button").Click();

        var trigger = cut.Find("button");
        var listbox = cut.Find("[role=listbox]");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("open", listbox.GetAttribute("data-state"));
        Assert.False(listbox.HasAttribute("hidden"));

        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));
        Assert.Equal(controls, listbox.GetAttribute("id"));
        Assert.Equal(trigger.GetAttribute("id"), listbox.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Selecting_an_option_reports_the_value_and_closes()
    {
        string? value = null;
        var cut = RenderComponent<BzSelect>(p => p
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => value = v))
            .AddChildContent(Body(Items(Item("apple", "Apple"), Item("banana", "Banana")))));

        cut.Find("button").Click(); // open (open state is uncontrolled here)
        cut.FindAll("[role=option]")[1].Click(); // pick "banana"

        Assert.Equal("banana", value);
        Assert.Equal("closed", cut.Find("[role=listbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void Disabled_option_marks_state_and_ignores_clicks()
    {
        string? value = null;
        var cut = RenderComponent<BzSelect>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => value = v))
            .AddChildContent(Body(Item("apple", "Apple", disabled: true))));

        var option = cut.Find("[role=option]");
        Assert.Equal("true", option.GetAttribute("aria-disabled"));
        Assert.True(option.HasAttribute("data-disabled"));

        option.Click();
        Assert.Null(value);
        Assert.Equal("open", cut.Find("[role=listbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void Selected_option_is_marked_aria_selected()
    {
        var cut = RenderComponent<BzSelect>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.DefaultValue, "banana")
            .AddChildContent(Body(Items(Item("apple", "Apple"), Item("banana", "Banana")))));

        var options = cut.FindAll("[role=option]");
        Assert.Equal("false", options[0].GetAttribute("aria-selected"));
        Assert.Equal("true", options[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Escape_on_the_listbox_requests_close()
    {
        var cut = RenderComponent<BzSelect>(p => p.AddChildContent(Body(Item("apple", "Apple"))));
        cut.Find("button").Click();
        Assert.Equal("open", cut.Find("[role=listbox]").GetAttribute("data-state"));

        cut.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("closed", cut.Find("[role=listbox]").GetAttribute("data-state"));
    }

    [Fact]
    public void Controlled_open_closes_via_handshake_then_hides()
    {
        var cut = RenderComponent<BzSelect>(p => p.Add(x => x.Open, true).AddChildContent(Body(Item("apple", "Apple"))));
        Assert.False(cut.Find("[role=listbox]").HasAttribute("hidden"));

        cut.SetParametersAndRender(p => p.Add(x => x.Open, false));
        // Closing: still mounted (no `hidden` yet), data-state flipped so the exit animation can play.
        var listbox = cut.Find("[role=listbox]");
        Assert.Equal("closed", listbox.GetAttribute("data-state"));
        Assert.False(listbox.HasAttribute("hidden"));

        // JS reports animationend -> the surface hides again (options stay mounted for registration).
        cut.InvokeAsync(() => cut.FindComponent<BzSelectContent>().Instance.OnCloseFinished());
        var hidden = cut.Find("[role=listbox]");
        Assert.True(hidden.HasAttribute("hidden"));
        Assert.Equal("closed", hidden.GetAttribute("data-state"));
    }
}
