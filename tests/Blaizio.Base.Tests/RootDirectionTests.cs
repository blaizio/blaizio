using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The direction set in <c>AddBlaizio()</c> is cascaded from the root of every component tree, so
/// each separately rendered interactive root on a page receives it (a provider in a statically
/// rendered layout cannot reach them). A provider still wins for its subtree, and an app that set
/// nothing gets nothing imposed.
/// </summary>
public class RootDirectionTests : BunitContext
{
    public RootDirectionTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // Shows what a component sees: the cascaded direction, the one it resolves, and the dir
    // attribute it would stamp on a surface that moves to the document body.
    private sealed class Probe : BzComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-cascaded", CascadedDirection?.ToAttribute() ?? "none");
            builder.AddAttribute(2, "data-resolved", ResolvedDirection.ToAttribute());
            builder.AddAttribute(3, "data-portal", PortalDirAttribute ?? "none");
            builder.AddAttribute(4, "data-own", DirAttribute ?? "none");
            builder.CloseElement();
        }
    }

    private static (string? Cascaded, string? Resolved, string? Portal) Read(AngleSharp.Dom.IElement probe) =>
        (probe.GetAttribute("data-cascaded"), probe.GetAttribute("data-resolved"), probe.GetAttribute("data-portal"));

    [Fact]
    public void An_app_that_set_no_direction_gets_none_cascaded()
    {
        Services.AddBlaizio();

        var probe = Render<Probe>().Find("span");

        // Body-level surfaces keep inheriting the document's own direction.
        Assert.Equal(("none", "ltr", "none"), Read(probe));
    }

    [Fact]
    public void A_configured_direction_reaches_a_component_with_no_provider_above_it()
    {
        Services.AddBlaizio(options => options.Direction = Direction.Rtl);

        var probe = Render<Probe>().Find("span");

        Assert.Equal(("rtl", "rtl", "rtl"), Read(probe));
        // It is the ambient direction, not this component's own: nothing is pinned on its element.
        Assert.Equal("none", probe.GetAttribute("data-own"));
    }

    [Fact]
    public void Setting_the_default_value_counts_as_setting_it()
    {
        Services.AddBlaizio(options => options.Direction = Direction.Ltr);

        Assert.Equal(("ltr", "ltr", "ltr"), Read(Render<Probe>().Find("span")));
    }

    [Fact]
    public void Every_separately_rendered_root_receives_it()
    {
        Services.AddBlaizio(options => options.Direction = Direction.Rtl);

        // Two roots with nothing in common but the container: what two interactive components
        // on one statically rendered page are.
        var first = Render<Probe>();
        var second = Render<Probe>();

        Assert.Equal("rtl", first.Find("span").GetAttribute("data-cascaded"));
        Assert.Equal("rtl", second.Find("span").GetAttribute("data-cascaded"));
    }

    [Fact]
    public void A_provider_still_decides_for_its_own_subtree()
    {
        Services.AddBlaizio(options => options.Direction = Direction.Rtl);

        var cut = Render<BaseDirectionProvider>(p => p
            .Add(x => x.Direction, Direction.Ltr)
            .AddChildContent<Probe>());

        Assert.Equal(("ltr", "ltr", "ltr"), Read(cut.Find("span")));
    }

    [Fact]
    public void The_first_registration_wins_and_registers_one_value()
    {
        // The supplier interface is internal to the framework: counted by its name. The test
        // host registers suppliers of its own, so the count is taken around the calls.
        int Suppliers() => Services.Count(d => d.ServiceType.Name == "ICascadingValueSupplier");
        var before = Suppliers();

        Services.AddBlaizio(options => options.Direction = Direction.Rtl);
        Services.AddBlaizio(options => options.Direction = Direction.Ltr);

        Assert.Equal(before + 1, Suppliers());
        Assert.Equal("rtl", Render<Probe>().Find("span").GetAttribute("data-cascaded"));
    }

    [Fact]
    public void Options_registered_by_hand_before_the_call_are_the_ones_cascaded()
    {
        Services.AddSingleton(new BlaizioOptions { Direction = Direction.Rtl });
        Services.AddBlaizio();

        Assert.Equal("rtl", Render<Probe>().Find("span").GetAttribute("data-cascaded"));
    }

    [Fact]
    public void Without_the_registration_nothing_changes()
    {
        Assert.Equal(("none", "ltr", "none"), Read(Render<Probe>().Find("span")));
    }

    [Fact]
    public async Task A_statically_rendered_component_receives_it_too()
    {
        var services = new ServiceCollection();
        services.AddBlaizio(options => options.Direction = Direction.Rtl);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Probe>()).ToHtmlString());

        Assert.Contains("data-cascaded=\"rtl\"", html);
    }

    [Fact]
    public void A_surface_that_moves_to_the_body_carries_the_configured_direction()
    {
        Services.AddBlaizio(options => options.Direction = Direction.Rtl);

        var cut = Render<BaseTooltip>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<BaseTooltipContent>());

        Assert.Equal("rtl", cut.Find("[role=tooltip]").GetAttribute("dir"));
    }
}
