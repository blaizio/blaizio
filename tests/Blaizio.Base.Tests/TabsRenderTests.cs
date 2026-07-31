using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + selection tests for the headless tabs. The roving-focus keyboard nav is JS (verified
/// in-browser); these cover the C# contract: roles, the trigger↔panel ARIA wiring, panel presence,
/// and click selection. JSInterop is Loose so the roving module import is a no-op.
/// </summary>
public class TabsRenderTests : BunitContext
{
    public TabsRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static RenderFragment TabsBody() => builder =>
    {
        var seq = 0;
        builder.OpenComponent<BaseTabsList>(seq++);
        builder.AddComponentParameter(seq++, nameof(BaseTabsList.ChildContent), (RenderFragment)(list =>
        {
            var s = 0;
            foreach (var value in new[] { "account", "password" })
            {
                list.OpenComponent<BaseTabsTrigger>(s++);
                list.AddComponentParameter(s++, nameof(BaseTabsTrigger.Value), value);
                list.AddComponentParameter(s++, nameof(BaseTabsTrigger.ChildContent),
                    (RenderFragment)(t => t.AddContent(0, value)));
                list.CloseComponent();
            }
        }));
        builder.CloseComponent();

        foreach (var value in new[] { "account", "password" })
        {
            builder.OpenComponent<BaseTabsContent>(seq++);
            builder.AddComponentParameter(seq++, nameof(BaseTabsContent.Value), value);
            builder.AddComponentParameter(seq++, nameof(BaseTabsContent.ChildContent),
                (RenderFragment)(c => c.AddContent(0, $"{value}-panel")));
            builder.CloseComponent();
        }
    };

    [Fact]
    public void Renders_tablist_of_tabs_with_aria_wiring_and_only_the_selected_panel()
    {
        var cut = Render<BaseTabs>(p => p
            .Add(x => x.DefaultValue, "account")
            .AddChildContent(TabsBody()));

        Assert.Equal("horizontal", cut.Find("[role=tablist]").GetAttribute("aria-orientation"));

        var tabs = cut.FindAll("[role=tab]");
        Assert.Equal(2, tabs.Count);
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("active", tabs[0].GetAttribute("data-state"));
        Assert.True(tabs[0].HasAttribute("data-roving-active"));
        Assert.False(tabs[0].HasAttribute("tabindex")); // owned by the roving-focus script
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));
        Assert.Equal("inactive", tabs[1].GetAttribute("data-state"));

        // Exactly one panel rendered, wired to its trigger both ways.
        var panels = cut.FindAll("[role=tabpanel]");
        Assert.Single(panels);
        Assert.Equal("account-panel", panels[0].TextContent);
        Assert.Equal(tabs[0].GetAttribute("aria-controls"), panels[0].GetAttribute("id"));
        Assert.Equal(tabs[0].GetAttribute("id"), panels[0].GetAttribute("aria-labelledby"));
        Assert.Equal("0", panels[0].GetAttribute("tabindex"));
    }

    [Fact]
    public void Clicking_a_tab_switches_the_panel()
    {
        var cut = Render<BaseTabs>(p => p
            .Add(x => x.DefaultValue, "account")
            .AddChildContent(TabsBody()));

        cut.FindAll("[role=tab]")[1].Click();

        Assert.Equal("password-panel", cut.Find("[role=tabpanel]").TextContent);
        Assert.Equal("true", cut.FindAll("[role=tab]")[1].GetAttribute("aria-selected"));
        Assert.Equal("false", cut.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void No_selection_renders_no_panel()
    {
        var cut = Render<BaseTabs>(p => p.AddChildContent(TabsBody()));

        Assert.Empty(cut.FindAll("[role=tabpanel]"));
    }

    [Fact]
    public void Disabled_trigger_does_not_select()
    {
        var cut = Render<BaseTabs>(p => p
            .Add(x => x.DefaultValue, "account")
            .AddChildContent((RenderFragment)(builder =>
            {
                var seq = 0;
                builder.OpenComponent<BaseTabsList>(seq++);
                builder.AddComponentParameter(seq++, nameof(BaseTabsList.ChildContent), (RenderFragment)(list =>
                {
                    list.OpenComponent<BaseTabsTrigger>(0);
                    list.AddComponentParameter(1, nameof(BaseTabsTrigger.Value), "account");
                    list.CloseComponent();
                    list.OpenComponent<BaseTabsTrigger>(2);
                    list.AddComponentParameter(3, nameof(BaseTabsTrigger.Value), "password");
                    list.AddComponentParameter(4, nameof(BaseTabsTrigger.Disabled), true);
                    list.CloseComponent();
                }));
                builder.CloseComponent();
            })));

        var disabled = cut.FindAll("[role=tab]")[1];
        Assert.True(disabled.HasAttribute("disabled"));
        disabled.Click();
        Assert.Equal("true", cut.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Controlled_tabs_do_not_self_update_but_raise_change()
    {
        string? changed = null;
        var cut = Render<BaseTabs>(p => p
            .Add(x => x.Value, "account")
            .Add(x => x.ValueChanged, (string v) => changed = v)
            .AddChildContent(TabsBody()));

        cut.FindAll("[role=tab]")[1].Click();

        // Controlled: stays on "account" until the parent flows a new Value back…
        Assert.Equal("account-panel", cut.Find("[role=tabpanel]").TextContent);
        // …but the intended value was announced.
        Assert.Equal("password", changed);
    }
}
