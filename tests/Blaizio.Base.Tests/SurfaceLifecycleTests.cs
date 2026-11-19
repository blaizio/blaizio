using System.Reflection;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// One script instance of a surface: claimed once each time the surface appears, given back when
/// it has closed, also when the instance is still on its way at that moment.
/// </summary>
public class SurfaceScriptTests
{
    private const string Module = "./_content/blaizio.base/dist/presence.js";
    private readonly SurfaceScriptRuntime _js = new();

    [Fact]
    public async Task A_claim_creates_one_instance_until_it_is_released()
    {
        var script = new SurfaceScript();
        Assert.False(script.Claimed);

        script.Claim(_js, Module, "createPresence");
        script.Claim(_js, Module, "createPresence"); // the next render
        Assert.True(script.Claimed);
        Assert.Single(_js.Instances);

        await script.ReleaseAsync();
        Assert.False(script.Claimed);
        Assert.True(_js.Instances[0].Released);
        Assert.Equal(["dispose"], _js.Instances[0].Calls);

        script.Claim(_js, Module, "createPresence"); // the surface appears again
        Assert.Equal(2, _js.Instances.Count);
        Assert.Single(_js.Live);
    }

    [Fact]
    public void A_factory_that_answers_nothing_is_not_asked_again_while_the_surface_stays()
    {
        _js.HoldFactory("createPresence");
        var script = new SurfaceScript();

        script.Claim(_js, Module, "createPresence");
        _js.LandNothing("createPresence"); // no anchor in the page, say
        script.Claim(_js, Module, "createPresence");

        Assert.True(script.Claimed);
        Assert.Equal(1, _js.Count("createPresence"));
    }

    [Fact]
    public async Task A_release_waits_for_a_creation_on_its_way_and_gives_back_what_it_made()
    {
        _js.HoldFactory("createPresence");
        var script = new SurfaceScript();
        script.Claim(_js, Module, "createPresence");

        var releasing = script.ReleaseAsync().AsTask();
        Assert.False(script.Claimed); // given up at once
        Assert.False(releasing.IsCompleted); // but the caller goes on only after the landing

        var late = _js.Land("createPresence");
        await releasing;
        Assert.True(late.Released);
        Assert.Equal(["dispose"], late.Calls);
    }

    [Fact]
    public async Task A_claim_made_during_a_release_gets_an_instance_of_its_own()
    {
        _js.HoldFactory("createPresence");
        var script = new SurfaceScript();
        script.Claim(_js, Module, "createPresence");
        var releasing = script.ReleaseAsync().AsTask();

        script.Claim(_js, Module, "createPresence"); // a render came in before the first one landed
        var stale = _js.Land("createPresence");
        var fresh = _js.Land("createPresence");
        await releasing;

        Assert.True(stale.Released);
        Assert.False(fresh.Released);
        Assert.True(script.Claimed);
    }

    [Fact]
    public async Task Releasing_several_gives_every_claim_up_before_the_first_one_is_done()
    {
        var first = new SurfaceScript();
        var second = new SurfaceScript();
        first.Claim(_js, Module, "createPresence");
        second.Claim(_js, Module, "createMenu");
        _js.Of("createPresence")[0].Hold("dispose");

        var releasing = SurfaceScript.ReleaseAllAsync([first, second]).AsTask();

        // The first release is still talking to the page, and the second is no longer claimed:
        // a render that comes in now creates both again, not only the first.
        Assert.False(releasing.IsCompleted);
        Assert.False(first.Claimed);
        Assert.False(second.Claimed);

        _js.Of("createPresence")[0].Complete("dispose");
        await releasing;
        Assert.Empty(_js.Live);
        Assert.Equal(
            ["createPresence#1.dispose", "createMenu#1.dispose", "createMenu#1 released", "createPresence#1 released"],
            _js.Log.Where(line => line.Contains('#')));
    }

    [Fact]
    public async Task Disposal_does_not_wait_and_a_late_instance_is_given_back_when_it_lands()
    {
        _js.HoldFactory("createPresence");
        var script = new SurfaceScript();
        script.Claim(_js, Module, "createPresence");

        await script.DisposeAsync(); // completes with the creation still out

        var late = _js.Land("createPresence");
        Assert.True(late.Released);
        script.Claim(_js, Module, "createPresence"); // a disposed script never creates again
        Assert.Equal(1, _js.Count("createPresence"));
    }

    [Fact]
    public async Task A_release_before_the_module_has_loaded_never_calls_the_factory()
    {
        _js.HoldImport("presence.js");
        var script = new SurfaceScript();
        script.Claim(_js, Module, "createPresence");

        var releasing = script.ReleaseAsync().AsTask();
        _js.LandImport("presence.js");
        await releasing;

        Assert.Empty(_js.Instances);
    }

    [Fact]
    public async Task A_call_waits_for_the_creation_and_does_nothing_once_released()
    {
        _js.HoldFactory("createPresence");
        var script = new SurfaceScript();
        await script.InvokeVoidAsync("onClosing"); // never claimed: nothing to call

        script.Claim(_js, Module, "createPresence");
        var calling = script.InvokeVoidAsync("onClosing").AsTask();
        Assert.False(calling.IsCompleted);
        var instance = _js.Land("createPresence");
        await calling;
        Assert.Equal(["onClosing"], instance.Calls);

        await script.ReleaseAsync();
        await script.InvokeVoidAsync("onClosing");
        Assert.False(instance.UsedAfterRelease);
    }
}

/// <summary>
/// The lifecycle every overlay surface shares (PresenceSurfaceBase, FloatingSurfaceBase), on a
/// surface made for the test: open, closing, closed, opened again on its way out, taken away at
/// any of those points, with the scripts answering late.
/// </summary>
public class SurfaceLifecycleTests : BunitContext
{
    private readonly SurfaceScriptRuntime _js = new();

    public SurfaceLifecycleTests() => Services.AddSingleton<IJSRuntime>(_js);

    private sealed class Surface : PresenceSurfaceBase
    {
        [Parameter] public bool Open { get; set; }
        [Parameter] public bool Drop { get; set; }
        [Parameter] public EventCallback OnClosed { get; set; }

        protected override bool IsOpen => Open;

        protected override async ValueTask OnClosedAsync() => await OnClosed.InvokeAsync();

        protected override void OnParametersSet()
        {
            if (Drop)
            {
                DropWithoutAnimation();
                return;
            }

            base.OnParametersSet();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!Present) return;
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-state", Closing ? "closed" : "open");
            builder.AddElementReferenceCapture(2, CaptureElement);
            builder.AddContent(3, "surface");
            builder.CloseElement();
        }
    }

    private sealed class Floating : FloatingSurfaceBase
    {
        [Parameter] public bool Open { get; set; }
        [Parameter] public Side Side { get; set; } = Side.Bottom;
        [Parameter] public Align Align { get; set; } = Align.Start;
        [Parameter] public bool WithLayer { get; set; }
        [Parameter] public bool Drop { get; set; }

        public int CloseRequests { get; private set; }

        protected override bool IsOpen => Open;
        protected override string AnchorSelector => "#anchor";
        protected override Side PreferredSide => Side;
        protected override Align PreferredAlign => Align;
        protected override double PreferredSideOffset => 6;
        protected override bool Dismissable => WithLayer;

        protected override Task RequestCloseAsync()
        {
            CloseRequests++;
            return Task.CompletedTask;
        }

        protected override void OnParametersSet()
        {
            if (Drop)
            {
                DropWithoutAnimation();
                return;
            }

            base.OnParametersSet();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!Present) return;
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-state", Closing ? "closed" : "open");
            builder.AddAttribute(2, "data-side", ResolvedSide);
            builder.AddAttribute(3, "data-align", ResolvedAlign);
            builder.AddElementReferenceCapture(4, CaptureElement);
            builder.CloseElement();
        }
    }

    private Task FinishAsync<T>(IRenderedComponent<T> cut) where T : PresenceSurfaceBase =>
        cut.InvokeAsync(() => cut.Instance.OnCloseFinished());

    // ---- open, closing, closed ----

    [Fact]
    public async Task A_surface_is_rendered_while_open_stays_while_it_closes_and_goes_when_the_script_says_so()
    {
        var closed = 0;
        var cut = Render<Surface>(p => p.Add(x => x.OnClosed, () => closed++));
        Assert.Equal("", cut.Markup);
        Assert.Empty(_js.Instances); // nothing is created for a surface that is not there

        cut.Render(p => p.Add(x => x.Open, true));
        Assert.Equal("open", cut.Find("div").GetAttribute("data-state"));
        var presence = Assert.Single(_js.Instances);
        Assert.Equal("createPresence", presence.Factory);
        Assert.NotNull(presence.Element!.Value.Id); // created for the rendered element

        cut.Render(p => p.Add(x => x.Open, false));
        Assert.Equal("closed", cut.Find("div").GetAttribute("data-state"));
        Assert.Equal(["onClosing"], presence.Calls);
        Assert.Equal(0, closed);

        await FinishAsync(cut);
        Assert.Equal("", cut.Markup);
        Assert.True(presence.Released);
        Assert.Equal(["onClosing", "dispose"], presence.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task A_close_report_for_a_surface_that_is_not_closing_changes_nothing()
    {
        var closed = 0;
        var cut = Render<Surface>(p => p.Add(x => x.Open, true).Add(x => x.OnClosed, () => closed++));

        await FinishAsync(cut); // a late report from an earlier close
        Assert.Equal("open", cut.Find("div").GetAttribute("data-state"));
        Assert.DoesNotContain(_js.Instances, i => i.Released);

        cut.Render(p => p.Add(x => x.Open, false));
        await FinishAsync(cut);
        await FinishAsync(cut); // reported twice
        Assert.Equal(1, closed);
        Assert.DoesNotContain(_js.Instances, i => i.ReleasedTwice);
    }

    [Fact]
    public async Task Opened_again_while_closing_it_keeps_its_element_and_its_scripts()
    {
        var closed = 0;
        var cut = Render<Surface>(p => p.Add(x => x.Open, true).Add(x => x.OnClosed, () => closed++));
        cut.Render(p => p.Add(x => x.Open, false));
        cut.Render(p => p.Add(x => x.Open, true));

        Assert.Equal("open", cut.Find("div").GetAttribute("data-state"));
        var presence = Assert.Single(_js.Instances);
        Assert.False(presence.Released);

        // The script may still report the close that was called off.
        await FinishAsync(cut);
        Assert.Equal("open", cut.Find("div").GetAttribute("data-state"));
        Assert.Equal(0, closed);

        cut.Render(p => p.Add(x => x.Open, false));
        await FinishAsync(cut);
        Assert.Equal("", cut.Markup);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Rendering_again_creates_nothing_twice_and_asks_about_the_exit_on_every_closing_render()
    {
        var cut = Render<Surface>(p => p.Add(x => x.Open, true));
        cut.Render();
        cut.Render();
        var presence = Assert.Single(_js.Instances);

        cut.Render(p => p.Add(x => x.Open, false));
        cut.Render();
        Assert.Single(_js.Instances);
        Assert.Equal(["onClosing", "onClosing"], presence.Calls);
    }

    [Fact]
    public async Task Each_appearance_gets_scripts_of_its_own()
    {
        var cut = Render<Surface>(p => p.Add(x => x.Open, true));
        cut.Render(p => p.Add(x => x.Open, false));
        await FinishAsync(cut);

        cut.Render(p => p.Add(x => x.Open, true));

        var instances = _js.Of("createPresence");
        Assert.Equal(2, instances.Count);
        Assert.True(instances[0].Released);
        Assert.False(instances[1].Released);
        Assert.NotEqual(instances[0].Element!.Value.Id, instances[1].Element!.Value.Id);
    }

    // ---- the scripts answer late ----

    [Fact]
    public async Task The_scripts_are_given_back_before_the_surface_is_rendered_away()
    {
        var closed = 0;
        var cut = Render<Surface>(p => p.Add(x => x.Open, true).Add(x => x.OnClosed, () => closed++));
        var presence = _js.Instances[0];
        cut.Render(p => p.Add(x => x.Open, false));
        presence.Hold("dispose");

        var finishing = FinishAsync(cut);

        // The release is still talking to the page: a script that moved the element elsewhere
        // has not put it back yet, so the element must still be rendered.
        Assert.Contains("surface", cut.Markup);
        Assert.Equal(0, closed);

        await cut.InvokeAsync(() => presence.Complete("dispose"));
        await finishing;
        Assert.Equal("", cut.Markup);
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task A_surface_that_closes_before_its_script_landed_asks_about_the_exit_when_it_lands()
    {
        _js.HoldFactory("createPresence");
        var cut = Render<Surface>(p => p.Add(x => x.Open, true));
        cut.Render(p => p.Add(x => x.Open, false));
        Assert.Equal(1, _js.Count("createPresence")); // claimed once, across both renders

        var presence = await cut.InvokeAsync(() => _js.Land("createPresence"));

        Assert.Equal(["onClosing"], presence.Calls);
    }

    [Fact]
    public async Task A_close_that_finishes_before_a_script_landed_waits_for_it_and_gives_it_back()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true));
        _js.HoldFactory("createPositioning");
        cut.Render(p => p.Add(x => x.Open, false));
        await FinishAsync(cut);
        cut.Render(p => p.Add(x => x.Open, true)); // positioning is asked for and stays out
        cut.Render(p => p.Add(x => x.Open, false));

        var finishing = FinishAsync(cut);
        Assert.NotEqual("", cut.Markup); // not removed while the script that moves it is unknown

        var late = await cut.InvokeAsync(() => _js.Land("createPositioning"));
        await finishing;

        Assert.True(late.Released);
        Assert.Equal("", cut.Markup);
        Assert.Empty(_js.Live);
    }

    // ---- the component goes away ----

    [Fact]
    public async Task Disposal_gives_every_script_back_while_open_and_while_closing()
    {
        var open = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        var closing = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        closing.Render(p => p.Add(x => x.Open, false));
        Assert.Equal(6, _js.Live.Count());

        await DisposeComponentsAsync();

        Assert.Empty(_js.Live);
        Assert.DoesNotContain(_js.Instances, i => i.ReleasedTwice || i.UsedAfterRelease);
    }

    [Fact]
    public async Task A_script_still_on_its_way_at_disposal_is_given_back_when_it_lands()
    {
        _js.HoldFactory("createPresence");
        Render<Surface>(p => p.Add(x => x.Open, true));

        await DisposeComponentsAsync();
        var late = await Renderer.Dispatcher.InvokeAsync(() => _js.Land("createPresence"));

        Assert.True(late.Released);
        Assert.Equal(["dispose"], late.Calls);
    }

    [Fact]
    public async Task A_module_that_loads_after_disposal_is_never_asked_for_an_instance()
    {
        _js.HoldImport("presence.js");
        Render<Surface>(p => p.Add(x => x.Open, true));

        await DisposeComponentsAsync();
        await Renderer.Dispatcher.InvokeAsync(() => _js.LandImport("presence.js"));

        Assert.Empty(_js.Instances);
    }

    [Fact]
    public async Task Disposal_in_the_middle_of_a_close_report_gives_nothing_back_twice()
    {
        var cut = Render<Surface>(p => p.Add(x => x.Open, true));
        var presence = _js.Instances[0];
        cut.Render(p => p.Add(x => x.Open, false));
        presence.Hold("dispose");
        var finishing = FinishAsync(cut);

        await DisposeComponentsAsync(); // a navigation, while the release is still out
        await Renderer.Dispatcher.InvokeAsync(() => presence.Complete("dispose"));
        await finishing;

        Assert.True(presence.Released);
        Assert.False(presence.ReleasedTwice);
        Assert.Equal(["onClosing", "dispose"], presence.Calls);
    }

    // ---- leaving with an ancestor, with no animation ----

    [Fact]
    public void A_dropped_surface_leaves_at_once_and_comes_back_with_scripts_for_its_new_element()
    {
        var closed = 0;
        var cut = Render<Surface>(p => p.Add(x => x.Open, true).Add(x => x.OnClosed, () => closed++));
        var first = _js.Instances[0];

        cut.Render(p => p.Add(x => x.Drop, true));
        Assert.Equal("", cut.Markup); // in that very render, no data-state="closed" in between
        Assert.True(first.Released);
        Assert.DoesNotContain("onClosing", first.Calls);
        Assert.Equal(0, closed);

        cut.Render(p => p.Add(x => x.Drop, false));
        Assert.Equal("open", cut.Find("div").GetAttribute("data-state"));
        var second = _js.Of("createPresence")[1];
        Assert.False(second.Released);
        Assert.NotEqual(first.Element!.Value.Id, second.Element!.Value.Id);
    }

    [Fact]
    public async Task A_dropped_surface_that_comes_back_at_once_gets_every_script_anew()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        var old = _js.Instances.ToList();
        var layer = _js.Of("createDismissableLayer")[0];
        layer.Hold("dispose"); // the layer goes first, and its release stays out

        cut.Render(p => p.Add(x => x.Drop, true));
        cut.Render(p => p.Add(x => x.Drop, false)); // back while the old scripts are still being given back

        // All three were given up before the first release was through, so the new element got
        // all three. Given up one by one, it would have been left with the first only.
        var fresh = _js.Instances.Except(old).ToList();
        Assert.Equal(
            ["createDismissableLayer", "createPositioning", "createPresence"],
            fresh.Select(i => i.Factory).Order());

        await cut.InvokeAsync(() => layer.Complete("dispose"));
        Assert.All(old, i => Assert.True(i.Released, i.Label));
        Assert.Equal(fresh.Select(i => i.Label).Order(), _js.Live.Select(i => i.Label).Order());
    }

    [Fact]
    public async Task A_surface_opened_again_while_its_scripts_are_being_given_back_ends_with_one_of_each()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        var layer = _js.Of("createDismissableLayer")[0];
        cut.Render(p => p.Add(x => x.Open, false));
        layer.Hold("dispose"); // the first release of the three stays out

        var finishing = FinishAsync(cut);
        cut.Render(p => p.Add(x => x.Open, true)); // open again before the teardown is through
        await cut.InvokeAsync(() => layer.Complete("dispose"));
        await finishing;

        Assert.Equal(
            ["createDismissableLayer", "createPositioning", "createPresence"],
            _js.Live.Select(i => i.Factory).Order());
        Assert.DoesNotContain(_js.Instances, i => i.ReleasedTwice || i.UsedAfterRelease);
    }

    // ---- floating surfaces ----

    [Fact]
    public void A_floating_surface_is_anchored_with_what_was_asked_for()
    {
        Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.Side, Side.Left).Add(x => x.Align, Align.End));

        Assert.Equal(["createPresence", "createPositioning"], _js.Instances.Select(i => i.Factory));
        var args = _js.Of("createPositioning")[0].Arguments;
        Assert.Equal("#anchor", args[0]);
        Assert.IsType<ElementReference>(args[1]);
        Assert.Equal(
            """{"side":"left","align":"end","sideOffset":6,"alignOffset":0,"collisionPadding":8,"inline":false}""",
            JsonSerializer.Serialize(args[2]));
    }

    [Fact]
    public async Task The_side_the_script_reports_is_rendered_and_an_unchanged_report_renders_nothing()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true));
        var renders = cut.RenderCount;

        await cut.InvokeAsync(() => cut.Instance.OnPlaced("top", "end"));
        Assert.Equal("top", cut.Find("div").GetAttribute("data-side"));
        Assert.Equal("end", cut.Find("div").GetAttribute("data-align"));
        Assert.Equal(renders + 1, cut.RenderCount);

        await cut.InvokeAsync(() => cut.Instance.OnPlaced("top", "end"));
        Assert.Equal(renders + 1, cut.RenderCount);
    }

    [Fact]
    public async Task Every_opening_starts_from_the_side_that_was_asked_for()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.Side, Side.Right).Add(x => x.Align, Align.End));
        // The first frame is painted before the positioning script has answered.
        Assert.Equal("right", cut.Find("div").GetAttribute("data-side"));
        Assert.Equal("end", cut.Find("div").GetAttribute("data-align"));

        await cut.InvokeAsync(() => cut.Instance.OnPlaced("left", "start")); // no room: it flipped
        cut.Render(p => p.Add(x => x.Open, false));
        await FinishAsync(cut);
        cut.Render(p => p.Add(x => x.Open, true));

        // A new opening, a new element, a new script: not where the last one ended up.
        Assert.Equal("right", cut.Find("div").GetAttribute("data-side"));
        Assert.Equal("end", cut.Find("div").GetAttribute("data-align"));
    }

    [Fact]
    public async Task Opened_again_while_closing_it_keeps_the_side_the_script_reported()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true));
        await cut.InvokeAsync(() => cut.Instance.OnPlaced("top", "center")); // flipped to stay in view

        cut.Render(p => p.Add(x => x.Open, false));
        cut.Render(p => p.Add(x => x.Open, true));

        // The positioning script is still the one from before and reports only a change, so
        // going back to "bottom" here would stay wrong for as long as the surface is open.
        Assert.Equal("top", cut.Find("div").GetAttribute("data-side"));
        Assert.Equal("center", cut.Find("div").GetAttribute("data-align"));
        Assert.Single(_js.Of("createPositioning"));
    }

    [Fact]
    public async Task A_layer_is_created_only_where_asked_for_and_its_dismissal_asks_the_root_to_close()
    {
        var plain = Render<Floating>(p => p.Add(x => x.Open, true));
        Assert.Empty(_js.Of("createDismissableLayer"));

        var cut = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        var layer = Assert.Single(_js.Of("createDismissableLayer"));
        Assert.Equal(
            """{"anchorSelector":"#anchor","dismissOnEscape":false}""",
            JsonSerializer.Serialize(layer.Arguments[2]));

        await cut.InvokeAsync(() => cut.Instance.OnDismissRequested());
        Assert.Equal(1, cut.Instance.CloseRequests);
        Assert.Equal(0, plain.Instance.CloseRequests);
    }

    [Fact]
    public async Task A_floating_surface_gives_the_layer_back_first_and_is_removed_last()
    {
        var cut = Render<Floating>(p => p.Add(x => x.Open, true).Add(x => x.WithLayer, true));
        cut.Render(p => p.Add(x => x.Open, false));
        _js.Log.Clear();

        await FinishAsync(cut);

        Assert.Equal(
            [
                "createDismissableLayer#1.dispose", "createDismissableLayer#1 released",
                "createPositioning#1.dispose", "createPositioning#1 released",
                "createPresence#1.dispose", "createPresence#1 released",
            ],
            _js.Log);
        Assert.Equal("", cut.Markup);
    }
}

/// <summary>
/// The real surfaces, each driven through its root: whatever a surface asks the page for, it
/// gives back, once, however its life ends. Dialog (window and backdrop), Popover, Hover Card,
/// Tooltip, Select, Combobox, and the three menus with a submenu.
/// </summary>
public class OverlaySurfaceScriptTests : BunitContext
{
    private readonly SurfaceScriptRuntime _js = new();

    public OverlaySurfaceScriptTests() => Services.AddSingleton<IJSRuntime>(_js);

    private sealed class Stage
    {
        public bool Open;
        public bool SubOpen;
        public bool Show = true;
        public Side? Side;
        public Align? Align;
    }

    private sealed class Host : ComponentBase
    {
        [Parameter] public Stage Stage { get; set; } = default!;
        [Parameter] public Func<Stage, RenderFragment> Body { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (Stage.Show) builder.AddContent(0, Body(Stage));
        }
    }

    private static RenderFragment Text(string text) => b => b.AddContent(0, text);

    private static RenderFragment All(params RenderFragment[] parts) => b =>
    {
        for (var i = 0; i < parts.Length; i++)
        {
            b.OpenRegion(i);
            parts[i](b);
            b.CloseRegion();
        }
    };

    private static RenderFragment Of<T>(RenderFragment? child, params (string Name, object? Value)[] parameters)
        where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        var seq = 1;
        foreach (var (name, value) in parameters)
        {
            if (value is not null) b.AddComponentParameter(seq, name, value);
            seq++;
        }

        if (child is not null) b.AddComponentParameter(seq, "ChildContent", child);
        b.CloseComponent();
    };

    private static (string, object?)[] Placement(Stage s) => [("Side", s.Side), ("Align", s.Align)];

    private static RenderFragment MenuItems(Stage s) => All(
        Of<BaseDropdownMenuItem>(Text("Profile")),
        Of<BaseDropdownMenuSub>(All(
                Of<BaseDropdownMenuSubTrigger>(Text("Share")),
                Of<BaseDropdownMenuSubContent>(Of<BaseDropdownMenuItem>(Text("Email")))),
            ("Open", (bool?)s.SubOpen)));

    private static Func<Stage, RenderFragment> Family(string name) => name switch
    {
        "dialog" => s => Of<BaseDialog>(All(
                Of<BaseDialogOverlay>(null),
                Of<BaseDialogContent>(Of<BaseDialogTitle>(Text("Title")))),
            ("Open", (bool?)s.Open)),
        "popover" => s => Of<BasePopover>(All(
                Of<BasePopoverTrigger>(Text("Trigger")),
                Of<BasePopoverContent>(Of<BasePopoverTitle>(Text("Title")), Placement(s))),
            ("Open", (bool?)s.Open)),
        "hover-card" => s => Of<BaseHoverCard>(All(
                Of<BaseHoverCardTrigger>(Text("Trigger")),
                Of<BaseHoverCardContent>(Text("Card"), Placement(s))),
            ("Open", (bool?)s.Open)),
        "tooltip" => s => Of<BaseTooltip>(All(
                Of<BaseTooltipTrigger>(Text("Trigger")),
                Of<BaseTooltipContent>(Text("Tip"), Placement(s))),
            ("Open", (bool?)s.Open)),
        "select" => s => Of<BaseSelect>(All(
                Of<BaseSelectTrigger>(Of<BaseSelectValue>(null)),
                Of<BaseSelectContent>(Of<BaseSelectItem>(Text("Apple"), ("Value", "apple")), Placement(s))),
            ("Open", (bool?)s.Open)),
        "combobox" => s => Of<BaseCombobox>(All(
                Of<BaseComboboxInput>(null),
                Of<BaseComboboxContent>(
                    Of<BaseComboboxList>(Of<BaseComboboxItem>(Text("Apple"), ("Value", "apple"))),
                    Placement(s))),
            ("Open", (bool?)s.Open)),
        "dropdown-menu" => s => Of<BaseDropdownMenu>(All(
                Of<BaseDropdownMenuTrigger>(Text("Menu")),
                Of<BaseDropdownMenuContent>(MenuItems(s), Placement(s))),
            ("Open", (bool?)s.Open)),
        "context-menu" => s => Of<BaseContextMenu>(All(
                Of<BaseContextMenuTrigger>(Text("Area")),
                Of<BaseContextMenuContent>(MenuItems(s))),
            ("Open", (bool?)s.Open)),
        "menubar" => s => Of<BaseMenubar>(
            Of<BaseMenubarMenu>(All(
                    Of<BaseMenubarTrigger>(Text("File")),
                    Of<BaseMenubarContent>(MenuItems(s), Placement(s))),
                ("Value", "file")),
            ("Value", s.Open ? "file" : ""),
            ("ValueChanged", EventCallback.Factory.Create<string?>(s, _ => { }))),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static TheoryData<string> Families =>
        ["dialog", "popover", "hover-card", "tooltip", "select", "combobox", "dropdown-menu", "context-menu", "menubar"];

    private static readonly string[] Factories =
        ["createPresence", "createPositioning", "createDismissableLayer", "createMenu", "createPortal", "createFocusScope"];

    private Stage _stage = new();

    private IRenderedComponent<Host> Start(string family, bool open = true, bool subOpen = false)
    {
        _stage = new Stage { Open = open, SubOpen = subOpen };
        return Render<Host>(p => p.Add(x => x.Stage, _stage).Add(x => x.Body, Family(family)));
    }

    private async Task SetAsync(IRenderedComponent<Host> cut, Action<Stage> change)
    {
        await cut.InvokeAsync(() => change(_stage));
        cut.Render();
    }

    // The script's report, to every surface on the page (a dialog has two: window and backdrop).
    private static async Task FinishAsync(IRenderedComponent<Host> cut)
    {
        var surfaces = cut.FindComponents<PresenceSurfaceBase>().Select(c => c.Instance).ToList();
        foreach (var surface in surfaces) await cut.InvokeAsync(surface.OnCloseFinished);
    }

    private void AssertNothingLeaked()
    {
        Assert.Empty(_js.Live.Select(i => i.Label));
        Assert.DoesNotContain(_js.Instances, i => i.ReleasedTwice);
        Assert.DoesNotContain(_js.Instances, i => i.UsedAfterRelease);
        Assert.DoesNotContain(_js.Log, line => line.EndsWith("MODULE RELEASED"));
    }

    public static TheoryData<string> PlacedFamilies =>
        ["popover", "hover-card", "tooltip", "select", "combobox", "dropdown-menu", "menubar"];

    [Theory, MemberData(nameof(PlacedFamilies))]
    public void A_surface_starts_from_the_side_and_alignment_it_was_asked_for(string family)
    {
        _stage = new Stage { Open = true, Side = Side.Top, Align = Align.End };
        var cut = Render<Host>(p => p.Add(x => x.Stage, _stage).Add(x => x.Body, Family(family)));

        var surface = cut.Find("[data-side]");
        Assert.Equal("top", surface.GetAttribute("data-side"));
        Assert.Equal("end", surface.GetAttribute("data-align"));
    }

    [Theory]
    [InlineData("dropdown-menu")]
    [InlineData("context-menu")]
    [InlineData("menubar")]
    public void A_submenu_starts_on_the_side_it_opens_to(string family)
    {
        var cut = Start(family, subOpen: true);

        // Root and submenu, in that order. The submenu opens beside its trigger, not below it.
        var menus = cut.FindAll("[role=menu]");
        Assert.Equal(2, menus.Count);
        Assert.Equal("bottom", menus[0].GetAttribute("data-side"));
        Assert.Equal("right", menus[1].GetAttribute("data-side"));
        Assert.Equal("start", menus[1].GetAttribute("data-align"));
    }

    [Theory, MemberData(nameof(PlacedFamilies))]
    public async Task A_surface_opened_again_while_closing_keeps_the_side_the_script_reported(string family)
    {
        var cut = Start(family);
        var surface = cut.FindComponents<FloatingSurfaceBase>()[0].Instance;
        await cut.InvokeAsync(() => surface.OnPlaced("left", "center"));

        await SetAsync(cut, s => s.Open = false);
        await SetAsync(cut, s => s.Open = true);

        var element = cut.Find("[data-side]");
        Assert.Equal("open", element.GetAttribute("data-state"));
        Assert.Equal("left", element.GetAttribute("data-side"));
        Assert.Equal("center", element.GetAttribute("data-align"));
    }

    // ---- what the scripts call back ----

    private static IEnumerable<Type> SurfaceTypes() =>
        typeof(PresenceSurfaceBase).Assembly.GetTypes().Where(t => typeof(PresenceSurfaceBase).IsAssignableFrom(t));

    [Fact]
    public void A_surface_that_declares_a_script_callback_makes_the_reference_with_its_own_type()
    {
        // The object reference keeps, in a trimmed app, the public methods of the type it is
        // created with. A callback declared further down the hierarchy is safe only if that class
        // creates the reference itself (CreateSelfReference).
        const BindingFlags declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var missing = SurfaceTypes()
            .Where(t => t.GetMethods(declared).Any(m => m.IsPublic && m.IsDefined(typeof(JSInvokableAttribute), false)))
            .Where(t => t.GetMethod("CreateSelfReference", declared) is null)
            .Select(t => t.Name);

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_surface_answers_the_callbacks_its_scripts_make()
    {
        // How script interop finds an instance method: public methods of the runtime type that
        // carry the attribute THEMSELVES. An override that leaves the attribute off drops out.
        foreach (var type in SurfaceTypes().Where(t => !t.IsAbstract))
        {
            var invokable = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.IsDefined(typeof(JSInvokableAttribute), false))
                .Select(m => m.Name)
                .ToHashSet();

            Assert.True(invokable.Contains("OnCloseFinished"), $"{type.Name}: OnCloseFinished");
            if (typeof(FloatingSurfaceBase).IsAssignableFrom(type))
            {
                Assert.True(invokable.Contains("OnPlaced"), $"{type.Name}: OnPlaced");
                Assert.True(invokable.Contains("OnDismissRequested"), $"{type.Name}: OnDismissRequested");
            }

            if (typeof(MenuContentBase).IsAssignableFrom(type))
                Assert.True(invokable.Contains("OnSubCloseRequested"), $"{type.Name}: OnSubCloseRequested");
        }

        Assert.Contains("OnDismissRequested", typeof(BaseDialogContent).GetMethods().Select(m => m.Name));
    }

    [Theory, MemberData(nameof(Families))]
    public async Task A_surface_that_has_closed_holds_no_script_and_opens_again_with_new_ones(string family)
    {
        var cut = Start(family, subOpen: true);
        var first = _js.Instances.Where(i => i.Factory != "createFocusScope" || family == "select").ToList();
        Assert.NotEmpty(first);

        await SetAsync(cut, s => s.Open = false);
        await FinishAsync(cut);

        // Scripts of the parts that stay on the page (the select's trigger, say) are not the
        // surface's: only what was created for the open surface has to be back.
        Assert.All(first.Where(i => Factories.Contains(i.Factory)), i => Assert.True(i.Released, i.Label));
        Assert.DoesNotContain(_js.Instances, i => i.ReleasedTwice || i.UsedAfterRelease);

        await SetAsync(cut, s => { s.Open = true; s.SubOpen = false; });
        Assert.Contains(_js.Live, i => i.Factory == "createPresence");

        await DisposeComponentsAsync();
        AssertNothingLeaked();
    }

    [Theory, MemberData(nameof(Families))]
    public async Task Rendering_again_and_opening_again_while_closing_create_nothing_new(string family)
    {
        var cut = Start(family, subOpen: true);
        var created = _js.Instances.Count;

        await SetAsync(cut, _ => { });
        await SetAsync(cut, _ => { });
        Assert.Equal(created, _js.Instances.Count);

        await SetAsync(cut, s => { s.Open = false; s.SubOpen = false; });
        await SetAsync(cut, s => s.Open = true);
        var surfaceScripts = _js.Instances.Where(i => i.Factory is "createPresence" or "createPositioning" or "createMenu").ToList();
        // The root surface kept what it had (a submenu left with its root and took its own along).
        Assert.Contains(surfaceScripts, i => i.Factory == "createPresence" && !i.Released);

        await DisposeComponentsAsync();
        AssertNothingLeaked();
    }

    [Theory, MemberData(nameof(Families))]
    public async Task A_surface_taken_away_while_open_or_while_closing_gives_everything_back(string family)
    {
        var open = Start(family, subOpen: true);
        await SetAsync(open, s => s.Show = false);
        AssertNothingLeaked();

        var closing = Start(family, subOpen: true);
        await SetAsync(closing, s => s.Open = false);
        await SetAsync(closing, s => s.Show = false);
        AssertNothingLeaked();
    }

    [Theory, MemberData(nameof(Families))]
    public async Task Scripts_that_land_after_the_surface_was_taken_away_are_given_back(string family)
    {
        foreach (var factory in Factories) _js.HoldFactory(factory);
        var cut = Start(family, subOpen: true);
        var asked = _js.Instances.Count;
        Assert.True(asked > 0);

        await SetAsync(cut, s => s.Show = false);
        await Renderer.Dispatcher.InvokeAsync(() =>
        {
            foreach (var factory in Factories)
            {
                while (_js.Instances.Any(i => i.Factory == factory && !i.Landed)) _js.Land(factory);
            }
        });

        Assert.Equal(asked, _js.Instances.Count); // nothing was asked for after the removal
        AssertNothingLeaked();
    }

    [Theory, MemberData(nameof(Families))]
    public async Task Modules_that_load_after_the_surface_was_taken_away_create_nothing(string family)
    {
        string[] modules = ["presence.js", "positioning.js", "dismissableLayer.js", "menu.js", "portal.js"];
        foreach (var module in modules) _js.HoldImport(module);
        var cut = Start(family, subOpen: true);

        await SetAsync(cut, s => s.Show = false);
        await Renderer.Dispatcher.InvokeAsync(() =>
        {
            foreach (var module in modules) _js.LandImport(module);
        });

        // The parts around the surface have scripts of their own; none of the surface's was made.
        Assert.DoesNotContain(_js.Instances, i => Factories.Contains(i.Factory) && i.Factory != "createFocusScope");
        AssertNothingLeaked();
    }
}
