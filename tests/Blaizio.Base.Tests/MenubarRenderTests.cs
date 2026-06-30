using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + open/switch tests for the headless menubar. Positioning, presence, menu navigation, and
/// roving focus are JS, verified in-browser; these cover the C# contract: the bar's role + roving
/// menuitem triggers, a trigger click opening its menu, the single open slot (opening one menu
/// closes the other), and controlled binding. JSInterop is Loose so module imports are no-ops.
/// Each menu reuses the shared dropdown surface, so items render as role="menuitem" inside role="menu".
/// </summary>
public class MenubarRenderTests : TestContext
{
    public MenubarRenderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // A bar of two menus (File / Edit), each a trigger + a content with one item. `value` (when set)
    // gives the menu a stable id so a controlled bar can open it by name.
    private static void AddMenu(RenderTreeBuilder b, int s, string? value, string label, string item)
    {
        b.OpenComponent<BaseMenubarMenu>(s);
        if (value is not null) b.AddComponentParameter(s + 1, nameof(BaseMenubarMenu.Value), value);
        b.AddComponentParameter(s + 2, nameof(BaseMenubarMenu.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BaseMenubarTrigger>(0);
            inner.AddComponentParameter(1, nameof(BaseMenubarTrigger.ChildContent),
                (RenderFragment)(t => t.AddContent(0, label)));
            inner.CloseComponent();
            inner.OpenComponent<BaseMenubarContent>(2);
            inner.AddComponentParameter(3, nameof(BaseMenubarContent.ChildContent), (RenderFragment)(c =>
            {
                c.OpenComponent<BaseDropdownMenuItem>(0);
                c.AddComponentParameter(1, nameof(BaseDropdownMenuItem.ChildContent),
                    (RenderFragment)(i => i.AddContent(0, item)));
                c.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        b.CloseComponent();
    }

    private static RenderFragment TwoMenus(string? fileValue = null, string? editValue = null) => b =>
    {
        AddMenu(b, 0, fileValue, "File", "New Tab");
        AddMenu(b, 100, editValue, "Edit", "Undo");
    };

    [Fact]
    public void Bar_renders_a_menubar_with_roving_menuitem_triggers_and_no_open_menu()
    {
        var cut = RenderComponent<BaseMenubar>(p => p.AddChildContent(TwoMenus()));

        Assert.Equal("menubar", cut.Find("[data-bz-menubar]").GetAttribute("role"));
        var triggers = cut.FindAll("[role=menuitem]");
        Assert.Equal(2, triggers.Count);
        Assert.All(triggers, t => Assert.True(t.HasAttribute("data-bz-roving-item")));
        Assert.All(triggers, t => Assert.Equal("closed", t.GetAttribute("data-state")));
        Assert.All(triggers, t => Assert.Equal("menu", t.GetAttribute("aria-haspopup")));
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Clicking_a_trigger_opens_its_menu()
    {
        var cut = RenderComponent<BaseMenubar>(p => p.AddChildContent(TwoMenus()));
        Assert.DoesNotContain("New Tab", cut.Markup);

        cut.FindAll("[role=menuitem]")[0].Click(); // File

        var content = cut.Find("[role=menu]");
        Assert.Equal("open", content.GetAttribute("data-state"));
        Assert.Contains("New Tab", cut.Markup);
        Assert.DoesNotContain("Undo", cut.Markup);
        Assert.Equal("true", cut.FindAll("[role=menuitem][data-bz-roving-item]")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Controlled_value_opens_the_matching_menu_and_switching_opens_the_other()
    {
        var cut = RenderComponent<BaseMenubar>(p => p
            .Add(x => x.Value, "file")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .AddChildContent(TwoMenus("file", "edit")));

        Assert.Contains("New Tab", cut.Markup);    // File open
        Assert.DoesNotContain("Undo", cut.Markup); // Edit closed

        cut.SetParametersAndRender(p => p.Add(x => x.Value, "edit"));

        Assert.Contains("Undo", cut.Markup);       // Edit now open
        // The File menu's surface is animating closed (still mounted until the presence handshake).
        Assert.Contains("closed", cut.FindComponents<BaseMenubarContent>()
            .Select(c => c.Find("[role=menu]").GetAttribute("data-state")));
    }

    [Fact]
    public void Bar_exposes_its_loop_setting_for_cross_menu_stepping()
    {
        // ts/menu.js reads data-bz-menubar-loop to decide whether stepping the inline arrows between
        // menus (from the root menu or any nested submenu) wraps past the ends; the actual switch is a
        // synthetic trigger click, exercised by Clicking_a_trigger_opens_its_menu and verified in-browser.
        var looping = RenderComponent<BaseMenubar>(p => p.AddChildContent(TwoMenus()));
        Assert.Equal("true", looping.Find("[data-bz-menubar]").GetAttribute("data-bz-menubar-loop"));

        var stopping = RenderComponent<BaseMenubar>(p => p.Add(x => x.Loop, false).AddChildContent(TwoMenus()));
        Assert.Equal("false", stopping.Find("[data-bz-menubar]").GetAttribute("data-bz-menubar-loop"));
    }

    [Fact]
    public void Closing_via_controlled_null_animates_the_open_menu_closed()
    {
        var cut = RenderComponent<BaseMenubar>(p => p
            .Add(x => x.Value, "file")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .AddChildContent(TwoMenus("file", "edit")));
        Assert.Single(cut.FindAll("[role=menu]"));

        cut.SetParametersAndRender(p => p.Add(x => x.Value, (string?)null));
        Assert.Equal("closed", cut.Find("[role=menu]").GetAttribute("data-state"));

        cut.InvokeAsync(() => cut.FindComponent<BaseMenubarContent>().Instance.OnCloseFinished());
        Assert.Empty(cut.FindAll("[role=menu]"));
    }
}
