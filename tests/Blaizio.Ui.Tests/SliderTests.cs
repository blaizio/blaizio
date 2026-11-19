using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>BzSlider: one thumb or a range, bound or left to itself, names, the value bubble, disabled.</summary>
public class SliderTests : BunitContext
{
    public SliderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    // What the slider script reports while a thumb is dragged or stepped with the keyboard.
    private static Task MoveAsync(IRenderedComponent<BzSlider> cut, double value, int thumb = 0)
    {
        var slider = cut.FindComponent<BaseSlider>();
        return slider.InvokeAsync(() => slider.Instance.OnInput(thumb, value));
    }

    private static Task ReleaseAsync(IRenderedComponent<BzSlider> cut)
    {
        var slider = cut.FindComponent<BaseSlider>();
        return slider.InvokeAsync(() => slider.Instance.OnCommit());
    }

    private static string[] Now(IRenderedComponent<BzSlider> cut) =>
        [.. cut.FindAll("[role=slider]").Select(thumb => thumb.GetAttribute("aria-valuenow") ?? "")];

    // ---- one thumb ----------------------------------------------------------------------------

    [Fact]
    public void With_nothing_set_there_is_one_thumb_at_the_minimum()
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.Min, 10).Add(x => x.Max, 50));

        var thumb = Assert.Single(cut.FindAll("[data-slot=slider-thumb]"));
        Assert.Equal("slider", thumb.GetAttribute("role"));
        Assert.Equal(("10", "50", "10"), (thumb.GetAttribute("aria-valuemin"), thumb.GetAttribute("aria-valuemax"), thumb.GetAttribute("aria-valuenow")));
        Assert.Single(cut.FindAll("[data-slot=slider] [data-slot=slider-track] [data-slot=slider-range]"));
    }

    [Fact]
    public async Task A_slider_left_to_itself_starts_at_DefaultValue_and_keeps_where_it_is_moved()
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.DefaultValue, 40));
        Assert.Equal(["40"], Now(cut));

        await MoveAsync(cut, 70);

        Assert.Equal(["70"], Now(cut));
        Assert.Contains("--bz-slider-thumb-fraction:0.7", cut.Find("[role=slider]").GetAttribute("style"));
    }

    [Fact]
    public async Task A_bound_slider_reports_each_move_and_shows_what_the_page_hands_back()
    {
        var values = new List<double>();
        var cut = Render<BzSlider>(p => p
            .Add(x => x.Value, 40)
            .Add(x => x.ValueChanged, (double value) => values.Add(value)));

        await MoveAsync(cut, 70);

        Assert.Equal([70], values);
        Assert.Equal(["40"], Now(cut));

        cut.Render(p => p.Add(x => x.Value, 70));
        Assert.Equal(["70"], Now(cut));
    }

    [Fact]
    public async Task The_commit_comes_once_when_the_thumb_is_let_go()
    {
        var commits = new List<IReadOnlyList<double>>();
        var cut = Render<BzSlider>(p => p
            .Add(x => x.DefaultValue, 10)
            .Add(x => x.OnValueCommit, (IReadOnlyList<double> final) => commits.Add(final)));

        await MoveAsync(cut, 20);
        await MoveAsync(cut, 30);
        Assert.Empty(commits);

        await ReleaseAsync(cut);

        Assert.Equal([30], Assert.Single(commits));
    }

    [Theory]
    [InlineData(42, "40")]   // snaps to the step
    [InlineData(43, "45")]
    [InlineData(500, "100")] // stops at the ends
    [InlineData(-20, "0")]
    public async Task A_move_lands_on_the_step_grid_inside_the_range(double proposed, string landed)
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.DefaultValue, 50).Add(x => x.Step, 5));

        await MoveAsync(cut, proposed);

        Assert.Equal([landed], Now(cut));
    }

    // ---- a range ------------------------------------------------------------------------------

    [Fact]
    public async Task DefaultValues_make_a_range_that_keeps_its_own_values()
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.DefaultValues, [20, 80]));
        Assert.Equal(["20", "80"], Now(cut));

        await MoveAsync(cut, 35, thumb: 0);
        await MoveAsync(cut, 60, thumb: 1);

        Assert.Equal(["35", "60"], Now(cut));
    }

    [Fact]
    public async Task A_bound_range_reports_both_values_on_each_move()
    {
        var reported = new List<IReadOnlyList<double>>();
        var cut = Render<BzSlider>(p => p
            .Add(x => x.Values, [20, 80])
            .Add(x => x.ValuesChanged, (IReadOnlyList<double> values) => reported.Add(values)));

        await MoveAsync(cut, 35, thumb: 0);

        Assert.Equal([35, 80], Assert.Single(reported));
        cut.Render(p => p.Add(x => x.Values, reported[0]));
        Assert.Equal(["35", "80"], Now(cut));
    }

    [Fact]
    public async Task Thumbs_keep_their_order_and_the_gap_asked_for()
    {
        var cut = Render<BzSlider>(p => p
            .Add(x => x.DefaultValues, [20, 80])
            .Add(x => x.Step, 5)
            .Add(x => x.MinStepsBetweenThumbs, 2));

        await MoveAsync(cut, 95, thumb: 0);
        Assert.Equal(["70", "80"], Now(cut));

        await MoveAsync(cut, 10, thumb: 1);
        Assert.Equal(["70", "80"], Now(cut));
    }

    [Fact]
    public void The_fill_spans_from_the_first_thumb_to_the_last()
    {
        var single = Render<BzSlider>(p => p.Add(x => x.DefaultValue, 40)).Find("[data-slot=slider-range]").GetAttribute("style")!;
        Assert.Contains("--bz-slider-range-start:0", single);
        Assert.Contains("--bz-slider-range-end:0.4", single);

        var range = Render<BzSlider>(p => p.Add(x => x.DefaultValues, [20, 80])).Find("[data-slot=slider-range]").GetAttribute("style")!;
        Assert.Contains("--bz-slider-range-start:0.2", range);
        Assert.Contains("--bz-slider-range-end:0.8", range);
    }

    // ---- names --------------------------------------------------------------------------------

    [Fact]
    public void The_name_goes_on_the_thumb_whichever_way_it_is_given()
    {
        var byParameter = Render<BzSlider>(p => p.Add(x => x.AriaLabel, "Volume"));
        Assert.Equal("Volume", byParameter.Find("[role=slider]").GetAttribute("aria-label"));
        Assert.False(byParameter.Find("[data-slot=slider]").HasAttribute("aria-label"));

        // A name splatted on the component belongs to the control, not to the box around it.
        var bySplat = Render<BzSlider>(p => p.AddUnmatched("aria-label", "Brightness").AddUnmatched("id", "brightness"));
        Assert.Equal("Brightness", bySplat.Find("[role=slider]").GetAttribute("aria-label"));
        var root = bySplat.Find("[data-slot=slider]");
        Assert.False(root.HasAttribute("aria-label"));
        Assert.Equal("brightness", root.Id);
    }

    [Fact]
    public void The_thumbs_of_a_range_take_their_own_names_and_fall_back_to_the_shared_one()
    {
        var cut = Render<BzSlider>(p => p
            .Add(x => x.DefaultValues, [10, 50, 90])
            .Add(x => x.AriaLabel, "Price")
            .Add(x => x.AriaLabels, ["Minimum", "Typical"]));

        Assert.Equal(["Minimum", "Typical", "Price"], cut.FindAll("[role=slider]").Select(thumb => thumb.GetAttribute("aria-label")));
    }

    // ---- the value bubble ---------------------------------------------------------------------

    [Fact]
    public void There_is_no_bubble_unless_asked_for()
    {
        Assert.Empty(Render<BzSlider>(p => p.Add(x => x.DefaultValue, 40)).FindAll("[data-slot=slider-tooltip]"));
    }

    [Fact]
    public async Task The_bubble_shows_the_value_of_its_own_thumb_and_follows_it()
    {
        var cut = Render<BzSlider>(p => p
            .Add(x => x.DefaultValues, [20, 80])
            .Add(x => x.TooltipMode, SliderTooltip.OnInteraction)
            .Add(x => x.TooltipFormat, value => $"{value}%"));

        string[] Bubbles() => [.. cut.FindAll("[data-slot=slider-tooltip]").Select(bubble => bubble.TextContent.Trim())];
        Assert.Equal(["20%", "80%"], Bubbles());
        // Hidden until the thumb is hovered or focused, and never in the way of the drag.
        var bubble = cut.Find("[data-slot=slider-tooltip]");
        Assert.Contains("opacity-0", bubble.ClassList);
        Assert.Contains("group-hover/slider-thumb:opacity-100", bubble.ClassList);
        Assert.Contains("group-focus/slider-thumb:opacity-100", bubble.ClassList);
        Assert.Contains("pointer-events-none", bubble.ClassList);
        Assert.Equal(cut.FindAll("[role=slider]")[0], bubble.ParentElement);

        await MoveAsync(cut, 45, thumb: 0);

        Assert.Equal(["45%", "80%"], Bubbles());
    }

    [Fact]
    public void An_always_on_bubble_is_visible_at_rest_and_writes_plain_numbers_by_default()
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.DefaultValue, 2.5).Add(x => x.Step, 0.5).Add(x => x.TooltipMode, SliderTooltip.Always));

        var bubble = cut.Find("[data-slot=slider-tooltip]");
        Assert.Equal("2.5", bubble.TextContent.Trim());
        Assert.Contains("opacity-100", bubble.ClassList);
        Assert.DoesNotContain("opacity-0", bubble.ClassList);
        Assert.NotNull(bubble.QuerySelector("[data-slot=slider-tooltip-arrow]"));
    }

    // ---- layout and state ---------------------------------------------------------------------

    [Fact]
    public void A_vertical_slider_says_so_and_places_its_parts_along_the_block_axis()
    {
        var cut = Render<BzSlider>(p => p
            .Add(x => x.Orientation, Orientation.Vertical)
            .Add(x => x.DefaultValue, 40)
            .Add(x => x.TooltipMode, SliderTooltip.Always));

        var thumb = cut.Find("[role=slider]");
        Assert.Equal("vertical", thumb.GetAttribute("aria-orientation"));
        Assert.Contains("inset-block-end:calc(var(--bz-slider-thumb-fraction)", thumb.GetAttribute("style"));
        Assert.Contains("height:calc(", cut.Find("[data-slot=slider-range]").GetAttribute("style"));
        Assert.Equal("vertical", cut.Find("[data-slot=slider]").GetAttribute("data-orientation"));
        // The bubble moves beside the thumb instead of above it.
        Assert.Contains("start-full", cut.Find("[data-slot=slider-tooltip]").ClassList);
    }

    [Fact]
    public void A_horizontal_slider_places_its_parts_along_the_inline_axis_so_it_mirrors_under_rtl()
    {
        var cut = Render<BzSlider>(p => p.Add(x => x.DefaultValue, 40).Add(x => x.TooltipMode, SliderTooltip.Always));

        Assert.Contains("inset-inline-start:calc(var(--bz-slider-thumb-fraction)", cut.Find("[role=slider]").GetAttribute("style"));
        Assert.Contains("inset-inline-start:calc(var(--bz-slider-range-start)", cut.Find("[data-slot=slider-range]").GetAttribute("style"));
        Assert.Contains("bottom-full", cut.Find("[data-slot=slider-tooltip]").ClassList);
    }

    [Fact]
    public async Task A_disabled_slider_does_not_move()
    {
        var values = new List<double>();
        var commits = 0;
        var cut = Render<BzSlider>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Value, 40)
            .Add(x => x.ValueChanged, (double value) => values.Add(value))
            .Add(x => x.OnValueCommit, (IReadOnlyList<double> _) => commits++));

        Assert.True(cut.Find("[data-slot=slider]").HasAttribute("data-disabled"));
        Assert.Equal("true", cut.Find("[role=slider]").GetAttribute("aria-disabled"));
        await MoveAsync(cut, 70);
        await ReleaseAsync(cut);

        Assert.Empty(values);
        Assert.Equal(0, commits);
    }

    [Theory]
    [InlineData(StatusColor.Default, null)]
    [InlineData(StatusColor.Success, "success")]
    [InlineData(StatusColor.Destructive, "destructive")]
    public void A_status_color_is_named_on_the_root(StatusColor color, string? name)
    {
        var root = Render<BzSlider>(p => p.Add(x => x.Color, color)).Find("[data-slot=slider]");

        Assert.Equal(name, root.GetAttribute("data-color"));
    }

    [Fact]
    public void Class_and_the_domain_reach_the_root()
    {
        var root = Render<BzSlider>(p => p
            .Add(x => x.Class, "w-64")
            .Add(x => x.Min, -1).Add(x => x.Max, 1).Add(x => x.Step, 0.1)
            .Add(x => x.Inverted, true)).Find("[data-slot=slider]");

        Assert.Contains("w-64", root.ClassList);
        Assert.Contains("bz-slider", root.ClassList);
        Assert.Equal(("-1", "1", "0.1"), (root.GetAttribute("data-min"), root.GetAttribute("data-max"), root.GetAttribute("data-step")));
        Assert.True(root.HasAttribute("data-inverted"));
    }
}
