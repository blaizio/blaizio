using AngleSharp.Dom;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// BzColorPicker and its parts: the color in and out in every format, alpha, the sliders and the
/// area, swatches, saved colors, the palette, the gradient and image surfaces, and disabled.
/// </summary>
public class ColorPickerTests : BunitContext
{
    public ColorPickerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private sealed class Log
    {
        public List<string> Values { get; } = [];
        public List<string> Commits { get; } = [];
    }

    // A picker that keeps its own color and reports every change and commit.
    private IRenderedComponent<BzColorPicker> Picker(
        Log log, string? start = null, Action<ComponentParameterCollectionBuilder<BzColorPicker>>? more = null) =>
        Render<BzColorPicker>(p =>
        {
            p.Add(x => x.DefaultValue, start)
                .Add(x => x.ValueChanged, (string value) => log.Values.Add(value))
                .Add(x => x.OnValueCommit, (string value) => log.Commits.Add(value));
            more?.Invoke(p);
        });

    private const string HueSlider = "data-color-slider=\"hue\"";
    private const string AlphaSlider = "data-color-slider=\"alpha\"";
    private const string StopBar = "data-slot=\"color-gradient-slider\"";

    // What the slider script reports while a thumb is dragged, and when it is let go.
    private static Task Slide(IRenderedComponent<IComponent> cut, string marker, double value, int thumb = 0)
    {
        var slider = cut.FindComponents<BaseSlider>().Single(s => s.Markup.Contains(marker));
        return slider.InvokeAsync(() => slider.Instance.OnInput(thumb, value));
    }

    private static Task Release(IRenderedComponent<IComponent> cut, string marker)
    {
        var slider = cut.FindComponents<BaseSlider>().Single(s => s.Markup.Contains(marker));
        return slider.InvokeAsync(() => slider.Instance.OnCommit());
    }

    // The same for the saturation and brightness square.
    private static Task Drag(IRenderedComponent<IComponent> cut, double saturation, double brightness)
    {
        var area = cut.FindComponent<BaseColorArea>();
        return area.InvokeAsync(() => area.Instance.OnInput(saturation, brightness));
    }

    private static void TypeInto(IRenderedComponent<IComponent> cut, string selector, string text, int index = 0)
    {
        cut.FindAll(selector)[index].Focus();
        cut.FindAll(selector)[index].Input(text);
        cut.FindAll(selector)[index].Blur();
    }

    private static void Choose(IRenderedComponent<IComponent> cut, string scope, string option) =>
        cut.FindAll($"{scope} [role=option]").Single(o => o.TextContent.Trim() == option).Click();

    private static string Shown(IRenderedComponent<IComponent> cut) =>
        cut.Find("input[data-slot=color-input]").GetAttribute("value")!;

    private static string HueOf(IRenderedComponent<IComponent> cut) =>
        cut.Find("[data-color-slider=hue] [role=slider]").GetAttribute("aria-valuenow")!;

    // ---- the color in and out -----------------------------------------------------------------

    [Fact]
    public void With_nothing_set_the_picker_starts_on_its_default_blue()
    {
        var cut = Render<BzColorPicker>();

        Assert.Equal("#2563eb", Shown(cut));
        Assert.Equal("Saturation 84%, Brightness 92%", cut.Find("[data-slot=color-area-thumb]").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void DefaultValue_is_read_in_any_format_and_shown_in_the_pickers_own()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.DefaultValue, "hsl(120 100% 50%)"));

        Assert.Equal("#00ff00", Shown(cut));
        Assert.Equal("120", HueOf(cut));
    }

    [Theory]
    [InlineData("rgb(0 255 0)")]
    [InlineData("00ff00")]
    [InlineData("0f0")]
    [InlineData("hsl(120 100% 50%)")]
    [InlineData("0 255 0")]
    public void A_color_typed_in_any_format_is_taken_and_committed(string typed)
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000");

        cut.Find("input[data-slot=color-input]").Change(typed);

        Assert.Equal(["#00ff00"], log.Values);
        Assert.Equal(["#00ff00"], log.Commits);
        Assert.Equal("#00ff00", Shown(cut));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("#12")]
    public void Typed_text_that_is_not_a_color_is_dropped(string typed)
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000");

        cut.Find("input[data-slot=color-input]").Change(typed);

        Assert.Empty(log.Values);
        Assert.Empty(log.Commits);
        Assert.Equal("#ff0000", Shown(cut));
    }

    [Fact]
    public void A_value_set_from_outside_is_shown_and_not_reported_back()
    {
        var log = new Log();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.Value, "#ff0000")
            .Add(x => x.ValueChanged, (string value) => log.Values.Add(value)));
        Assert.Equal("#ff0000", Shown(cut));

        cut.Render(p => p.Add(x => x.Value, "rgb(0 0 255)"));

        Assert.Equal("#0000ff", Shown(cut));
        Assert.Equal("240", HueOf(cut));
        Assert.Empty(log.Values);
    }

    [Fact]
    public void A_bound_picker_hands_each_change_to_the_page_and_follows_what_the_page_sets()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "#ff0000").Add(x => x.Swatches, ["#00ff00"]));

        cut.Find("[data-slot=color-swatches] [data-slot=color-swatch]").Click();
        Assert.Equal("#00ff00", cut.Instance.Value);
        Assert.Equal(["#00ff00"], cut.Instance.Commits);

        cut.InvokeAsync(() => cut.Instance.Set(host => host.Value = "#0000ff"));
        Assert.Equal("#0000ff", Shown(cut));
        Assert.Equal(["#00ff00"], cut.Instance.Commits);
    }

    // ---- formats ------------------------------------------------------------------------------

    [Theory]
    [InlineData(ColorFormat.Hex, "#00ff00")]
    [InlineData(ColorFormat.Rgb, "rgb(0 255 0)")]
    [InlineData(ColorFormat.Rgba, "rgba(0, 255, 0, 1)")]
    [InlineData(ColorFormat.Hsl, "hsl(120 100% 50%)")]
    [InlineData(ColorFormat.Hsla, "hsla(120, 100%, 50%, 1)")]
    [InlineData(ColorFormat.Hsb, "hsb(120 100% 100%)")]
    [InlineData(ColorFormat.Hwb, "hwb(120 0% 0%)")]
    [InlineData(ColorFormat.Cmyk, "device-cmyk(100% 0% 100% 0%)")]
    [InlineData(ColorFormat.Oklch, "oklch(0.866 0.295 142.5)")]
    [InlineData(ColorFormat.Oklab, "oklab(0.866 -0.234 0.179)")]
    public void The_value_is_written_in_the_format_the_picker_is_set_to(ColorFormat format, string green)
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.Format, format).Add(x => x.Swatches, ["#00ff00"]));

        cut.Find("[data-slot=color-swatch]").Click();

        Assert.Equal([green], log.Values);
        Assert.Equal([green], log.Commits);
    }

    [Theory]
    [InlineData(ColorFormat.Rgb, new[] { "Red", "Green", "Blue" }, new[] { "37", "99", "235" })]
    [InlineData(ColorFormat.Rgba, new[] { "Red", "Green", "Blue" }, new[] { "37", "99", "235" })]
    [InlineData(ColorFormat.Hsl, new[] { "Hue", "Saturation", "Lightness" }, new[] { "221°", "83%", "53%" })]
    [InlineData(ColorFormat.Hsb, new[] { "Hue", "Saturation", "Brightness" }, new[] { "221°", "84%", "92%" })]
    [InlineData(ColorFormat.Hwb, new[] { "Hue", "Whiteness", "Blackness" }, new[] { "221°", "15%", "8%" })]
    [InlineData(ColorFormat.Cmyk, new[] { "Cyan", "Magenta", "Yellow", "Black" }, new[] { "84%", "58%", "0%", "8%" })]
    [InlineData(ColorFormat.Oklch, new[] { "Lightness", "Chroma", "Hue" }, new[] { "0.546", "0.215", "263°" })]
    [InlineData(ColorFormat.Oklab, new[] { "Lightness", "A axis", "B axis" }, new[] { "0.546", "-0.027", "-0.214" })]
    public void Every_format_but_hex_shows_one_named_field_a_channel(ColorFormat format, string[] names, string[] values)
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.Format, format));

        var fields = cut.FindAll("input[data-slot=color-channel]");
        Assert.Equal(names, fields.Select(f => f.GetAttribute("aria-label")));
        Assert.Equal(values, fields.Select(f => f.GetAttribute("value")));
        Assert.All(fields, f => Assert.Equal("spinbutton", f.GetAttribute("role")));
        Assert.Empty(cut.FindAll("input[data-slot=color-input]"));
    }

    [Fact]
    public void Hex_shows_one_free_text_field()
    {
        var cut = Render<BzColorPicker>();

        Assert.Equal("Color value", cut.Find("input[data-slot=color-input]").GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("input[data-slot=color-channel]"));
    }

    [Fact]
    public void Editing_one_channel_changes_that_channel_only()
    {
        var log = new Log();
        var cut = Picker(log, "#2563eb", p => p.Add(x => x.Format, ColorFormat.Rgb));

        TypeInto(cut, "input[data-slot=color-channel]", "200", index: 1);

        Assert.Equal("rgb(37 200 235)", log.Values[^1]);
        Assert.Equal("rgb(37 200 235)", log.Commits[^1]);
        Assert.Equal(["37", "200", "235"], cut.FindAll("input[data-slot=color-channel]").Select(f => f.GetAttribute("value")));
    }

    [Fact]
    public void An_arrow_key_steps_a_channel()
    {
        var log = new Log();
        var cut = Picker(log, "#2563eb", p => p.Add(x => x.Format, ColorFormat.Rgb));

        cut.FindAll("input[data-slot=color-channel]")[0].KeyDown(Key.Up);

        Assert.Equal("rgb(38 99 235)", log.Values[^1]);
    }

    [Fact]
    public void Picking_a_format_rewrites_the_value_in_it_and_shows_its_channels()
    {
        var log = new Log();
        var formats = new List<ColorFormat>();
        var cut = Picker(log, "#ff0000", p => p
            .Add(x => x.ShowFormatPicker, true)
            .Add(x => x.FormatChanged, (ColorFormat format) => formats.Add(format)));
        Assert.Equal("Color format", cut.Find("[data-slot=color-format] [role=combobox]").GetAttribute("aria-label"));
        Assert.Equal(
            ["HEX", "RGB", "RGBA", "HSL", "HSLA", "HSB", "HWB", "CMYK", "OKLCH", "OKLab"],
            cut.FindAll("[data-slot=color-format] [role=option]").Select(o => o.TextContent.Trim()));

        Choose(cut, "[data-slot=color-format]", "HSL");

        Assert.Equal([ColorFormat.Hsl], formats);
        // Written in the new format at once, before the page has handed the format back.
        Assert.Equal(["hsl(0 100% 50%)"], log.Values);
        Assert.Equal(["hsl(0 100% 50%)"], log.Commits);
    }

    [Fact]
    public void A_picker_left_to_itself_keeps_the_format_that_was_picked()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.DefaultValue, "#ff0000").Add(x => x.ShowFormatPicker, true));

        Choose(cut, "[data-slot=color-format]", "RGB");

        Assert.Equal(["255", "0", "0"], cut.FindAll("input[data-slot=color-channel]").Select(f => f.GetAttribute("value")));
        Assert.Equal("RGB", cut.Find("[data-slot=color-format] [data-slot=select-value]").TextContent.Trim());
    }

    [Fact]
    public void A_bound_format_follows_the_page()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "#ff0000"));

        Choose(cut, "[data-slot=color-format]", "HSB");
        Assert.Equal(ColorFormat.Hsb, cut.Instance.Format);
        Assert.Equal("hsb(0 100% 100%)", cut.Instance.Value);

        cut.InvokeAsync(() => cut.Instance.Set(host => host.Format = ColorFormat.Hex));
        Assert.Equal("#ff0000", Shown(cut));
    }

    [Fact]
    public void A_perceptual_value_keeps_its_own_numbers_until_the_color_really_moves()
    {
        var log = new Log();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.DefaultValue, "oklch(0.7 0.185 285)")
            .Add(x => x.Format, ColorFormat.Oklch)
            .Add(x => x.ShowFormatPicker, true)
            .Add(x => x.Swatches, ["#00ff00"])
            .Add(x => x.ValueChanged, (string value) => log.Values.Add(value)));
        Assert.Equal(["0.7", "0.185", "285°"], cut.FindAll("input[data-slot=color-channel]").Select(f => f.GetAttribute("value")));

        // Away to another format and back: the numbers that were written, not re-derived ones.
        Choose(cut, "[data-slot=color-format]", "HEX");
        Choose(cut, "[data-slot=color-format]", "OKLCH");
        Assert.Equal("oklch(0.7 0.185 285)", log.Values[^1]);

        // One channel edited keeps the two others as written.
        TypeInto(cut, "input[data-slot=color-channel]", "0.6", index: 0);
        Assert.Equal("oklch(0.6 0.185 285)", log.Values[^1]);

        // A swatch is a real move: from here on the numbers are the model's.
        cut.Find("[data-slot=color-swatch]").Click();
        Assert.Equal("oklch(0.866 0.295 142.5)", log.Values[^1]);
    }

    // ---- alpha --------------------------------------------------------------------------------

    [Fact]
    public void Without_alpha_the_picker_shows_and_writes_opaque_colors()
    {
        var log = new Log();
        var cut = Picker(log, "#ff000080", p => p.Add(x => x.Swatches, ["#00ff0080"]));

        Assert.Empty(cut.FindAll("[data-color-slider=alpha]"));
        Assert.Empty(cut.FindAll("input[data-slot=color-opacity]"));
        Assert.Equal("#ff0000", Shown(cut));

        cut.Find("[data-slot=color-swatch]").Click();
        Assert.Equal(["#00ff00"], log.Values);
    }

    [Fact]
    public async Task With_alpha_the_slider_and_the_percentage_field_both_show_and_set_it()
    {
        var log = new Log();
        var cut = Picker(log, "#ff000080", p => p.Add(x => x.ShowAlpha, true));

        Assert.Equal("#ff000080", Shown(cut));
        var thumb = cut.Find("[data-color-slider=alpha] [role=slider]");
        Assert.Equal("Opacity", thumb.GetAttribute("aria-label"));
        Assert.StartsWith("0.50", thumb.GetAttribute("aria-valuenow") + "0");
        var field = cut.Find("input[data-slot=color-opacity]");
        Assert.Equal("Opacity percentage", field.GetAttribute("aria-label"));
        Assert.Equal("50%", field.GetAttribute("value"));

        await Slide(cut, AlphaSlider, 0.25);
        Assert.Equal("#ff000040", log.Values[^1]);
        Assert.Equal("25%", cut.Find("input[data-slot=color-opacity]").GetAttribute("value"));

        TypeInto(cut, "input[data-slot=color-opacity]", "100");
        Assert.Equal("#ff0000", log.Values[^1]);
        Assert.Equal("#ff0000", log.Commits[^1]);
    }

    [Fact]
    public void A_swatch_brings_its_own_alpha_when_the_picker_has_alpha()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.ShowAlpha, true).Add(x => x.Swatches, ["#00ff0080"]));

        cut.Find("[data-slot=color-swatch]").Click();

        Assert.Equal(["#00ff0080"], log.Values);
    }

    [Fact]
    public void The_percentage_field_can_be_left_out()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.ShowAlpha, true).Add(x => x.ShowOpacityInput, false));

        Assert.Single(cut.FindAll("[data-color-slider=alpha]"));
        Assert.Empty(cut.FindAll("input[data-slot=color-opacity]"));
    }

    // ---- the sliders and the area -------------------------------------------------------------

    [Fact]
    public async Task The_hue_slider_turns_the_color_and_reports_once_it_is_let_go()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000");
        Assert.Equal("Hue", cut.Find("[data-color-slider=hue] [role=slider]").GetAttribute("aria-label"));

        await Slide(cut, HueSlider, 120);
        Assert.Equal(["#00ff00"], log.Values);
        Assert.Empty(log.Commits);

        await Release(cut, HueSlider);
        Assert.Equal(["#00ff00"], log.Commits);
    }

    [Fact]
    public async Task The_end_of_the_hue_slider_is_red_again_and_the_thumb_stays_at_the_end()
    {
        var log = new Log();
        var cut = Picker(log, "#00ff00");

        await Slide(cut, HueSlider, 360);

        Assert.Equal(["#ff0000"], log.Values);
        Assert.Equal("360", HueOf(cut));
    }

    [Fact]
    public async Task The_area_sets_saturation_and_brightness()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000");
        var thumb = cut.Find("[data-slot=color-area-thumb]");
        Assert.Equal("Color", thumb.GetAttribute("aria-label"));
        Assert.Equal("Saturation 100%, Brightness 100%", thumb.GetAttribute("aria-valuetext"));

        await Drag(cut, 0.5, 1);

        Assert.Equal(["#ff8080"], log.Values);
        Assert.Equal("Saturation 50%, Brightness 100%", cut.Find("[data-slot=color-area-thumb]").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public async Task The_hue_survives_a_trip_through_black()
    {
        var log = new Log();
        var cut = Picker(log, "#00ff00");

        await Drag(cut, 0.5, 0);
        Assert.Equal("#000000", log.Values[^1]);
        Assert.Equal("120", HueOf(cut));

        await Drag(cut, 1, 1);
        Assert.Equal("#00ff00", log.Values[^1]);
    }

    [Fact]
    public async Task The_hue_survives_black_when_the_page_hands_the_value_back()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "#00ff00"));

        await Drag(cut, 0.5, 0);
        Assert.Equal("#000000", cut.Instance.Value);
        // Bound: the picker has been re-rendered with "#000000", which names no hue.
        Assert.Equal("120", HueOf(cut));

        await Drag(cut, 1, 1);
        Assert.Equal("#00ff00", cut.Instance.Value);
    }

    [Fact]
    public void The_part_names_can_be_set()
    {
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.ShowAlpha, true)
            .AddChildContent<BzColorArea>(a => a
                .Add(x => x.AriaLabel, "Farbe").Add(x => x.XAxisLabel, "Sättigung").Add(x => x.YAxisLabel, "Helligkeit"))
            .AddChildContent<BzColorHueSlider>(s => s.Add(x => x.AriaLabel, "Farbton"))
            .AddChildContent<BzColorAlphaSlider>(s => s.Add(x => x.AriaLabel, "Deckkraft"))
            .AddChildContent<BzColorInput>(i => i.Add(x => x.AriaLabel, "Farbwert")));

        var thumb = cut.Find("[data-slot=color-area-thumb]");
        Assert.Equal("Farbe", thumb.GetAttribute("aria-label"));
        Assert.Equal("Sättigung 84%, Helligkeit 92%", thumb.GetAttribute("aria-valuetext"));
        Assert.Equal("Farbton", cut.Find("[data-color-slider=hue] [role=slider]").GetAttribute("aria-label"));
        Assert.Equal("Deckkraft", cut.Find("[data-color-slider=alpha] [role=slider]").GetAttribute("aria-label"));
        Assert.Equal("Farbwert", cut.Find("input[data-slot=color-input]").GetAttribute("aria-label"));
    }

    // ---- swatches, saved colors, palette ------------------------------------------------------

    [Fact]
    public void Swatches_apply_their_color_and_the_matching_one_is_marked()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.Swatches, ["#ff0000", "rgb(0 255 0)", "#0000ff"]));

        var group = cut.Find("[data-slot=color-swatches]");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("Color swatches", group.GetAttribute("aria-label"));
        IEnumerable<string?> Pressed() => cut.FindAll("[data-slot=color-swatch]").Select(s => s.GetAttribute("aria-pressed"));
        Assert.Equal(["#ff0000", "rgb(0 255 0)", "#0000ff"], cut.FindAll("[data-slot=color-swatch]").Select(s => s.GetAttribute("aria-label")));
        Assert.Equal(["true", "false", "false"], Pressed());

        cut.FindAll("[data-slot=color-swatch]")[1].Click();

        Assert.Equal(["#00ff00"], log.Values);
        Assert.Equal(["#00ff00"], log.Commits);
        Assert.Equal(["false", "true", "false"], Pressed());
        Assert.True(cut.FindAll("[data-slot=color-swatch]")[1].HasAttribute("data-selected"));
    }

    [Fact]
    public void A_swatch_that_is_not_a_color_does_nothing()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.Swatches, ["tomato"]));

        cut.Find("[data-slot=color-swatch]").Click();

        Assert.Empty(log.Values);
        Assert.Equal("false", cut.Find("[data-slot=color-swatch]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_swatch_saved_in_a_format_no_browser_paints_is_still_painted()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.Swatches, ["hsb(0 100% 100%)"]));

        Assert.Contains("linear-gradient(rgb(255 0 0), rgb(255 0 0))", cut.Find("[data-slot=color-swatch]").GetAttribute("style"));
    }

    [Fact]
    public void The_saved_panel_banks_the_current_color_once_and_removes_it_again()
    {
        var saved = new List<IReadOnlyList<string>>();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.DefaultValue, "#ff0000")
            .Add(x => x.ShowSaved, true)
            .Add(x => x.Swatches, ["#00ff00"])
            .Add(x => x.SavedColors, [])
            .Add(x => x.SavedColorsChanged, (IReadOnlyList<string> list) => saved.Add(list)));
        Assert.Equal("Add current color", cut.Find("[data-slot=color-saved-add]").GetAttribute("aria-label"));

        cut.Find("[data-slot=color-saved-add]").Click();
        Assert.Equal(["#ff0000"], saved[^1]);

        cut.Render(p => p.Add(x => x.SavedColors, saved[^1]));
        Assert.Single(cut.FindAll("[data-slot=color-saved] [data-slot=color-swatch]"));

        // The same color again is not banked twice.
        cut.Find("[data-slot=color-saved-add]").Click();
        Assert.Single(saved);

        cut.Find("[data-slot=color-swatches] [data-slot=color-swatch]").Click();
        cut.Find("[data-slot=color-saved-add]").Click();
        Assert.Equal(["#ff0000", "#00ff00"], saved[^1]);

        cut.Render(p => p.Add(x => x.SavedColors, saved[^1]));
        var remove = cut.FindAll("[data-slot=color-saved-remove]");
        Assert.Equal(["Remove #ff0000", "Remove #00ff00"], remove.Select(r => r.GetAttribute("aria-label")));
        remove[0].Click();
        Assert.Equal(["#00ff00"], saved[^1]);
    }

    [Fact]
    public void A_color_already_saved_in_another_format_is_not_saved_again()
    {
        var saved = new List<IReadOnlyList<string>>();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.DefaultValue, "#ff0000")
            .Add(x => x.ShowSaved, true)
            .Add(x => x.SavedColors, ["rgb(255 0 0)"])
            .Add(x => x.SavedColorsChanged, (IReadOnlyList<string> list) => saved.Add(list)));

        cut.Find("[data-slot=color-saved-add]").Click();

        Assert.Empty(saved);
    }

    [Fact]
    public void A_picker_left_to_itself_keeps_its_own_saved_list_and_a_saved_color_applies()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.ShowSaved, true).Add(x => x.Swatches, ["#00ff00"]));

        cut.Find("[data-slot=color-saved-add]").Click();
        cut.Find("[data-slot=color-swatches] [data-slot=color-swatch]").Click();
        Assert.Equal("false", cut.Find("[data-slot=color-saved] [data-slot=color-swatch]").GetAttribute("aria-pressed"));

        cut.Find("[data-slot=color-saved] [data-slot=color-swatch]").Click();

        Assert.Equal("#ff0000", log.Values[^1]);
        Assert.Equal("true", cut.Find("[data-slot=color-saved] [data-slot=color-swatch]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_bound_saved_list_reaches_the_page()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "#ff0000"));

        cut.Find("[data-slot=color-saved-add]").Click();

        Assert.Equal(["#ff0000"], cut.Instance.Saved);
        Assert.Single(cut.FindAll("[data-slot=color-saved] [data-slot=color-swatch]"));
    }

    [Fact]
    public void A_palette_is_named_rows_of_swatches()
    {
        var log = new Log();
        IReadOnlyList<ColorPaletteGroup> groups =
        [
            new("Red", ["#fecaca", "#ff0000"]),
            new("Blue", ["#bfdbfe", "#0000ff"]),
        ];
        var cut = Picker(log, "#ff0000", p => p.AddChildContent<BzColorPalette>(palette => palette.Add(x => x.Groups, groups)));

        Assert.Equal("Color palette", cut.Find("[data-slot=color-palette]").GetAttribute("aria-label"));
        var rows = cut.FindAll("[data-slot=color-palette-group]");
        Assert.Equal(2, rows.Count);
        // Each row is a group named by its visible heading.
        Assert.All(rows, row =>
        {
            var heading = row.QuerySelector("[data-slot=color-palette-label]")!;
            Assert.Equal("group", row.GetAttribute("role"));
            Assert.Equal(heading.Id, row.GetAttribute("aria-labelledby"));
            Assert.False(row.HasAttribute("aria-label"));
        });
        Assert.Equal(["Red", "Blue"], cut.FindAll("[data-slot=color-palette-label]").Select(l => l.TextContent));
        Assert.Equal("true", rows[0].QuerySelectorAll("[data-slot=color-swatch]")[1].GetAttribute("aria-pressed"));

        cut.FindAll("[data-slot=color-palette-group]")[1].QuerySelectorAll("[data-slot=color-swatch]")[1].Click();

        Assert.Equal(["#0000ff"], log.Values);
    }

    [Fact]
    public void A_palette_without_headings_keeps_the_row_names_for_assistive_tech()
    {
        IReadOnlyList<ColorPaletteGroup> groups = [new("Red", ["#ff0000"])];
        var cut = Render<BzColorPicker>(p => p.AddChildContent<BzColorPalette>(
            palette => palette.Add(x => x.Groups, groups).Add(x => x.ShowLabels, false)));

        var row = cut.Find("[data-slot=color-palette-group]");
        Assert.Equal("Red", row.GetAttribute("aria-label"));
        Assert.False(row.HasAttribute("aria-labelledby"));
        Assert.Empty(cut.FindAll("[data-slot=color-palette-label]"));
    }

    // ---- surfaces -----------------------------------------------------------------------------

    [Theory]
    [InlineData(true, false, false, 0)]
    [InlineData(false, false, false, 0)] // everything off is still a solid picker
    [InlineData(true, true, false, 2)]
    [InlineData(true, true, true, 3)]
    [InlineData(false, true, true, 2)]
    public void The_switch_appears_once_there_is_more_than_one_surface(bool solid, bool gradient, bool image, int tabs)
    {
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.ShowSolid, solid).Add(x => x.ShowGradient, gradient).Add(x => x.ShowImage, image));

        Assert.Equal(tabs, cut.FindAll("[data-slot=color-mode-tab]").Count);
        if (!gradient && !image) Assert.Single(cut.FindAll("[data-slot=color-area]"));
    }

    [Fact]
    public void Switching_to_gradient_starts_a_ramp_from_the_color_on_screen()
    {
        var log = new Log();
        var modes = new List<ColorPickerMode>();
        var cut = Picker(log, "#ff0000", p => p
            .Add(x => x.ShowGradient, true)
            .Add(x => x.ModeChanged, (ColorPickerMode mode) => modes.Add(mode)));
        var group = cut.Find("[data-slot=color-mode-tabs]");
        Assert.Equal("radiogroup", group.GetAttribute("role"));
        Assert.Equal("Fill type", group.GetAttribute("aria-label"));
        Assert.Equal(["Solid", "Gradient"], cut.FindAll("[role=radio]").Select(t => t.TextContent.Trim()));
        Assert.Equal(["true", "false"], cut.FindAll("[role=radio]").Select(t => t.GetAttribute("aria-checked")));
        Assert.Empty(cut.FindAll("[data-slot=color-gradient-bar]"));

        cut.FindAll("[role=radio]")[1].Click();

        Assert.Equal([ColorPickerMode.Gradient], modes);
        Assert.Equal(["linear-gradient(180deg, #ff0000 0%, #ff000000 100%)"], log.Values);
        Assert.Equal(log.Values, log.Commits);
        Assert.Equal(["false", "true"], cut.FindAll("[role=radio]").Select(t => t.GetAttribute("aria-checked")));
        Assert.Equal(2, cut.FindAll("[data-slot=color-gradient-thumb]").Count);

        // The same tab again changes nothing.
        cut.FindAll("[role=radio]")[1].Click();
        Assert.Single(modes);
    }

    [Fact(Skip = "bug: BzColorPicker does not raise GradientChanged when the switch to the gradient surface creates the ramp")]
    public void Switching_to_gradient_hands_the_new_ramp_to_a_bound_gradient()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "#ff0000"));

        cut.FindAll("[role=radio]")[1].Click();

        Assert.Equal("linear-gradient(180deg, #ff0000 0%, #ff000000 100%)", cut.Instance.Value);
        Assert.NotNull(cut.Instance.Gradient);
    }

    [Fact]
    public void A_gradient_value_opens_the_gradient_surface_on_its_first_stop()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));

        Assert.Equal(["false", "true"], cut.FindAll("[role=radio]").Select(t => t.GetAttribute("aria-checked")));
        var thumbs = cut.FindAll("[data-slot=color-gradient-thumb]");
        Assert.Equal(["Stop 1", "Stop 2"], thumbs.Select(t => t.GetAttribute("aria-label")));
        Assert.Equal(["0", "100"], thumbs.Select(t => t.GetAttribute("aria-valuenow")));
        Assert.True(thumbs[0].HasAttribute("data-active"));
        Assert.Equal("Gradient stops", cut.Find("[data-slot=color-gradient-slider]").GetAttribute("aria-label"));
        Assert.Equal("#ff0000", Shown(cut));
        // The model behind the string reaches a bound Gradient without an edit.
        Assert.Equal(GradientType.Linear, cut.Instance.Gradient!.Type);
        Assert.Equal(90, cut.Instance.Gradient.Angle);
        Assert.Equal(["#ff0000", "#0000ff"], cut.Instance.Gradient.Stops.Select(stop => stop.Color));
    }

    [Fact(Skip = "bug: BzColorPicker does not raise ModeChanged when the shape of Value moves it to another surface, so a bound Mode stays on Solid")]
    public void A_gradient_value_reports_the_gradient_surface_to_a_bound_mode()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));

        Assert.Equal(ColorPickerMode.Gradient, cut.Instance.Mode);
    }

    [Fact]
    public async Task On_the_gradient_surface_the_color_parts_edit_the_selected_stop()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));

        await Slide(cut, HueSlider, 120);
        Assert.Equal("linear-gradient(90deg, #00ff00 0%, #0000ff 100%)", cut.Instance.Value);
        Assert.Equal(["#00ff00", "#0000ff"], cut.Instance.Gradient!.Stops.Select(stop => stop.Color));

        // Focus is how a stop is selected, by pointer and by keyboard alike.
        cut.FindAll("[data-slot=color-gradient-thumb]")[1].TriggerEvent("onfocusin", new FocusEventArgs());
        Assert.True(cut.FindAll("[data-slot=color-gradient-thumb]")[1].HasAttribute("data-active"));
        Assert.False(cut.FindAll("[data-slot=color-gradient-thumb]")[0].HasAttribute("data-active"));
        Assert.Equal("#0000ff", Shown(cut));

        cut.Find("input[data-slot=color-input]").Change("#ffffff");
        Assert.Equal("linear-gradient(90deg, #00ff00 0%, #ffffff 100%)", cut.Instance.Value);
    }

    [Fact]
    public async Task Dragging_a_stop_moves_it_and_selects_it()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));

        await Slide(cut, StopBar, 60, thumb: 1);

        Assert.Equal("linear-gradient(90deg, #ff0000 0%, #0000ff 60%)", cut.Instance.Value);
        Assert.Equal([0, 0.6], cut.Instance.Gradient!.Stops.Select(stop => stop.Position));
        Assert.True(cut.FindAll("[data-slot=color-gradient-thumb]")[1].HasAttribute("data-active"));
        Assert.Equal("#0000ff", Shown(cut));

        await Release(cut, StopBar);
        Assert.Equal(["linear-gradient(90deg, #ff0000 0%, #0000ff 60%)"], cut.Instance.Commits);
    }

    [Fact]
    public async Task A_stop_is_added_in_the_widest_gap_with_the_color_the_ramp_has_there()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));
        Assert.Equal("Add stop", cut.Find("[data-slot=color-gradient-add]").GetAttribute("aria-label"));

        cut.Find("[data-slot=color-gradient-add]").Click();

        Assert.Equal("linear-gradient(90deg, #ff0000 0%, #800080 50%, #0000ff 100%)", cut.Instance.Value);
        // The new stop is the one being edited.
        Assert.True(cut.FindAll("[data-slot=color-gradient-thumb]")[1].HasAttribute("data-active"));
        Assert.Equal("#800080", Shown(cut));

        await Slide(cut, StopBar, 80, thumb: 1);
        cut.Find("[data-slot=color-gradient-add]").Click();
        // 0, 80, 100: the room is between the first two.
        Assert.Equal([0, 0.4, 0.8, 1], cut.Instance.Gradient!.Sorted.Select(stop => stop.Position));
    }

    [Fact]
    public void The_selected_stop_can_be_removed_down_to_two()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #00ff00 50%, #0000ff 100%)"));
        var remove = cut.Find("[data-slot=color-gradient-remove]");
        Assert.Equal("Remove selected stop", remove.GetAttribute("aria-label"));
        Assert.False(remove.HasAttribute("disabled"));

        cut.FindAll("[data-slot=color-gradient-thumb]")[1].TriggerEvent("onfocusin", new FocusEventArgs());
        cut.Find("[data-slot=color-gradient-remove]").Click();

        Assert.Equal("linear-gradient(90deg, #ff0000 0%, #0000ff 100%)", cut.Instance.Value);
        // Two ends are the least a ramp can have.
        Assert.True(cut.Find("[data-slot=color-gradient-remove]").HasAttribute("disabled"));
        Assert.Equal("#0000ff", Shown(cut));
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Backspace")]
    public void Delete_on_a_stop_removes_it_and_never_the_last_two(string key)
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #00ff00 50%, #0000ff 100%)"));

        cut.FindAll("[data-slot=color-gradient-thumb]")[0].KeyDown(key);
        Assert.Equal("linear-gradient(90deg, #00ff00 50%, #0000ff 100%)", cut.Instance.Value);

        cut.FindAll("[data-slot=color-gradient-thumb]")[0].KeyDown(key);
        Assert.Equal("linear-gradient(90deg, #00ff00 50%, #0000ff 100%)", cut.Instance.Value);
    }

    [Theory]
    [InlineData("Radial", "radial-gradient(circle at center, #ff0000 0%, #0000ff 100%)", true)]
    [InlineData("Angular", "conic-gradient(from 90deg at center, #ff0000 0%, #0000ff 100%)", false)]
    [InlineData("Diamond", null, true)]
    public void The_shape_can_be_changed_and_only_the_directional_ones_take_an_angle(string shape, string? paint, bool noAngle)
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));
        Assert.Equal("Gradient type", cut.Find("[data-slot=color-gradient-type] [role=combobox]").GetAttribute("aria-label"));
        Assert.False(cut.Find("input[data-slot=color-gradient-angle]").HasAttribute("disabled"));

        Choose(cut, "[data-slot=color-gradient-type]", shape);

        Assert.Equal(Enum.Parse<GradientType>(shape), cut.Instance.Gradient!.Type);
        Assert.Equal(paint ?? cut.Instance.Gradient.ToCss(), cut.Instance.Value);
        Assert.Equal(noAngle, cut.Find("input[data-slot=color-gradient-angle]").HasAttribute("disabled"));
    }

    [Fact]
    public void The_angle_field_turns_the_ramp()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));
        var field = cut.Find("input[data-slot=color-gradient-angle]");
        Assert.Equal("Gradient angle", field.GetAttribute("aria-label"));
        Assert.Equal("90°", field.GetAttribute("value"));

        TypeInto(cut, "input[data-slot=color-gradient-angle]", "45");

        Assert.Equal("linear-gradient(45deg, #ff0000 0%, #0000ff 100%)", cut.Instance.Value);
        Assert.Equal("linear-gradient(45deg, #ff0000 0%, #0000ff 100%)", cut.Instance.Commits[^1]);
    }

    [Fact]
    public void Back_on_solid_the_value_is_the_stop_that_was_being_edited_and_the_ramp_is_kept()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));
        cut.FindAll("[data-slot=color-gradient-thumb]")[1].TriggerEvent("onfocusin", new FocusEventArgs());

        cut.FindAll("[role=radio]")[0].Click();

        Assert.Equal(ColorPickerMode.Solid, cut.Instance.Mode);
        Assert.Equal("#0000ff", cut.Instance.Value);
        Assert.Empty(cut.FindAll("[data-slot=color-gradient-bar]"));

        cut.FindAll("[role=radio]")[1].Click();
        Assert.Equal("linear-gradient(90deg, #ff0000 0%, #0000ff 100%)", cut.Instance.Value);
    }

    [Fact]
    public void A_plain_color_set_from_outside_leaves_the_gradient_surface()
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.Start, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));

        cut.InvokeAsync(() => cut.Instance.Set(host => host.Value = "#00ff00"));

        Assert.Empty(cut.FindAll("[data-slot=color-gradient-bar]"));
        Assert.Equal(["true", "false"], cut.FindAll("[role=radio]").Select(t => t.GetAttribute("aria-checked")));
        Assert.Equal("#00ff00", Shown(cut));
    }

    private static readonly GradientValue ThreeStops = new(GradientType.Radial, 180,
        [new("#ff0000", 0), new("#00ff00", 0.5), new("#0000ff", 1)]);

    [Fact]
    public void A_gradient_handed_in_as_a_model_opens_the_gradient_surface()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.ShowGradient, true));

        cut.Render(p => p.Add(x => x.Gradient, ThreeStops));

        Assert.Equal(3, cut.FindAll("[data-slot=color-gradient-thumb]").Count);
        Assert.Equal("Radial", cut.Find("[data-slot=color-gradient-type] [data-slot=select-value]").TextContent.Trim());
        Assert.Contains("linear-gradient(to right, #ff0000 0%, #00ff00 50%, #0000ff 100%)",
            cut.Find("[data-slot=color-gradient-track]").GetAttribute("style"));
        Assert.Equal("#ff0000", Shown(cut));
    }

    [Fact(Skip = "bug: BzColorPicker drops a Gradient handed in on the first render: the default color is read after it and puts the picker back on Solid")]
    public void A_gradient_handed_in_from_the_start_opens_the_gradient_surface()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.ShowGradient, true).Add(x => x.Gradient, ThreeStops));

        Assert.Equal(3, cut.FindAll("[data-slot=color-gradient-thumb]").Count);
    }

    [Fact]
    public void A_surface_that_is_turned_off_cannot_stay_selected()
    {
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.ShowGradient, true)
            .Add(x => x.Value, "linear-gradient(90deg, #ff0000 0%, #0000ff 100%)"));
        Assert.Single(cut.FindAll("[data-slot=color-gradient-bar]"));

        cut.Render(p => p.Add(x => x.ShowGradient, false));

        Assert.Empty(cut.FindAll("[data-slot=color-gradient-bar]"));
        Assert.Single(cut.FindAll("[data-slot=color-area]"));
    }

    // ---- the image surface --------------------------------------------------------------------

    private IRenderedComponent<ColorPickerHost> WithImage(ImageFillValue? image = null)
    {
        var cut = Render<ColorPickerHost>(p => p.Add(x => x.ShowImage, true).Add(x => x.Start, "#ff0000"));
        cut.InvokeAsync(() => cut.Instance.Set(host => host.Image = image ?? new ImageFillValue("/photo.png")));
        return cut;
    }

    [Fact]
    public void An_image_handed_in_opens_the_image_surface_and_paints_it()
    {
        var cut = WithImage();

        Assert.Equal(["false", "false", "true"], cut.FindAll("[role=radio]").Select(t => t.GetAttribute("aria-checked")));
        Assert.Equal("background:url(/photo.png) center/cover no-repeat;filter:none",
            cut.Find("[data-slot=color-image-preview]").GetAttribute("style"));
        // The color parts stand down: this surface edits a picture.
        Assert.Empty(cut.FindAll("[data-slot=color-area]"));
        Assert.Empty(cut.FindAll("[data-slot=color-image-choose]"));
    }

    [Fact]
    public void An_image_paint_as_the_value_opens_the_image_surface_with_its_fit()
    {
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.ShowImage, true)
            .Add(x => x.Value, "url(/photo.png) center/contain no-repeat"));

        Assert.Contains("url(/photo.png) center/contain no-repeat", cut.Find("[data-slot=color-image-preview]").GetAttribute("style"));
        Assert.Equal("Fit", cut.Find("[data-slot=color-image] [data-slot=select-value]").TextContent.Trim());
    }

    [Fact]
    public void With_no_picture_the_surface_asks_for_one_and_its_controls_wait()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.ShowImage, true).Add(x => x.Mode, ColorPickerMode.Image));

        Assert.Equal("Drop an image or click to choose", cut.Find("[data-slot=color-image-choose]").TextContent.Trim());
        Assert.True(cut.Find("[data-slot=color-image-reset]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-slot=color-image-rotate]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-slot=color-image] [role=combobox]").HasAttribute("disabled"));
        var sliders = cut.FindAll("[data-slot=color-image-adjustments] [role=slider]");
        Assert.Equal(
            ["Exposure", "Contrast", "Saturation", "Temperature", "Tint", "Highlights", "Shadows"],
            sliders.Select(s => s.GetAttribute("aria-label")));
        Assert.All(sliders, s => Assert.Equal("true", s.GetAttribute("aria-disabled")));
    }

    [Fact]
    public async Task A_tone_slider_grades_the_picture_through_the_filter()
    {
        var cut = WithImage();

        await Slide(cut, "aria-label=\"Exposure\"", 50);

        Assert.Equal(0.5, cut.Instance.Image!.Exposure);
        Assert.Equal("/photo.png", cut.Instance.Image.Src);
        Assert.Contains("filter:brightness(1.5)", cut.Find("[data-slot=color-image-preview]").GetAttribute("style"));
        // The paint is unchanged: a grade is a filter, not part of the background.
        Assert.Equal("url(/photo.png) center/cover no-repeat", cut.Instance.Value);
    }

    [Fact]
    public async Task The_adjustments_css_has_no_function_for_come_with_a_filter_the_model_points_at()
    {
        var cut = WithImage();
        Assert.Empty(cut.FindAll("filter"));

        await Slide(cut, "aria-label=\"Temperature\"", 100);

        var image = cut.Instance.Image!;
        Assert.Equal(1, image.Temperature);
        var filter = cut.Find("filter");
        Assert.Equal(image.FilterId, filter.Id);
        Assert.Equal(image.ColorMatrix(), filter.QuerySelector("feColorMatrix")!.GetAttribute("values"));
        Assert.Equal($"url(#{filter.Id})", image.ToFilterCss());
    }

    [Fact]
    public void Reset_clears_the_grade_and_keeps_the_picture()
    {
        var cut = WithImage(new ImageFillValue("/photo.png", ImageFillFit.Fit, Exposure: 0.4, Tint: 0.2));
        Assert.Equal("Reset adjustments", cut.Find("[data-slot=color-image-reset]").GetAttribute("aria-label"));

        cut.Find("[data-slot=color-image-reset]").Click();

        var image = cut.Instance.Image!;
        Assert.Equal(("/photo.png", ImageFillFit.Fit, 0d, 0d), (image.Src, image.Fit, image.Exposure, image.Tint));
        Assert.Empty(cut.FindAll("filter"));
    }

    [Theory]
    [InlineData("Tile", "url(/photo.png) left top/50% repeat")]
    [InlineData("Fit", "url(/photo.png) center/contain no-repeat")]
    [InlineData("Crop", "url(/photo.png) center/auto no-repeat")]
    public void The_fit_changes_the_paint(string fit, string paint)
    {
        var cut = WithImage();
        Assert.Equal("Image fit", cut.Find("[data-slot=color-image] [role=combobox]").GetAttribute("aria-label"));

        Choose(cut, "[data-slot=color-image]", fit);

        Assert.Equal(paint, cut.Instance.Value);
        Assert.Equal(Enum.Parse<ImageFillFit>(fit), cut.Instance.Image!.Fit);
        Assert.Equal(paint, cut.Instance.Commits[^1]);
    }

    [Fact]
    public void Rotating_swaps_in_the_rotated_bitmap_the_script_made()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/imageFill.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string?>("rotateImage", _ => true).SetResult("blob:rotated");
        var cut = WithImage();
        // The decoder was handed the picture that was already in the model.
        Assert.Equal("/photo.png", module.Invocations["showImage"].Single().Arguments[1]);
        Assert.Equal("Rotate image", cut.Find("[data-slot=color-image-rotate]").GetAttribute("aria-label"));

        cut.Find("[data-slot=color-image-rotate]").Click();

        Assert.Equal("blob:rotated", cut.Instance.Image!.Src);
        Assert.Equal("url(blob:rotated) center/cover no-repeat", cut.Instance.Value);
    }

    [Fact]
    public async Task A_bound_export_is_baked_once_the_edits_settle()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/imageFill.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string?>("exportImage", _ => true).SetResult("data:image/png;base64,AAAA");
        var exports = new List<string?>();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.ShowImage, true)
            .Add(x => x.Image, new ImageFillValue("/photo.png"))
            .Add(x => x.ImageExportChanged, (string? data) => exports.Add(data)));

        await Slide(cut, "aria-label=\"Exposure\"", 10);
        await Slide(cut, "aria-label=\"Exposure\"", 20);
        Assert.Empty(exports);

        // Nothing re-renders the picker when the export lands, so this waits on the list itself.
        Assert.True(SpinWait.SpinUntil(() => exports.Count > 0, TimeSpan.FromSeconds(5)));
        Assert.Equal(["data:image/png;base64,AAAA"], exports);
        // Two edits in a row, one bake: the earlier one was dropped when the later one landed.
        Assert.Single(module.Invocations["exportImage"]);
    }

    // ---- disabled -----------------------------------------------------------------------------

    [Fact]
    public async Task A_disabled_picker_shows_its_color_and_takes_no_input()
    {
        var log = new Log();
        var modes = new List<ColorPickerMode>();
        var cut = Picker(log, "#ff0000", p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.ShowAlpha, true)
            .Add(x => x.ShowGradient, true)
            .Add(x => x.ShowSaved, true)
            .Add(x => x.ShowFormatPicker, true)
            .Add(x => x.Swatches, ["#00ff00"])
            .Add(x => x.ModeChanged, (ColorPickerMode mode) => modes.Add(mode)));

        Assert.True(cut.Find("[data-slot=color-picker]").HasAttribute("data-disabled"));
        Assert.Equal("#ff0000", Shown(cut));
        Assert.True(cut.Find("input[data-slot=color-input]").HasAttribute("disabled"));
        Assert.True(cut.Find("input[data-slot=color-opacity]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-slot=color-format] [role=combobox]").HasAttribute("disabled"));
        Assert.All(cut.FindAll("[data-slot=color-swatch], [data-slot=color-saved-add], [data-slot=color-mode-tab]"),
            button => Assert.True(button.HasAttribute("disabled")));
        Assert.All(cut.FindAll("[role=slider]"), thumb => Assert.Equal("true", thumb.GetAttribute("aria-disabled")));

        // What a script would still report is ignored too.
        await Slide(cut, HueSlider, 120);
        await Slide(cut, AlphaSlider, 0.2);
        await Drag(cut, 0.2, 0.2);
        await Release(cut, HueSlider);

        Assert.Empty(log.Values);
        Assert.Empty(log.Commits);
        Assert.Empty(modes);
    }

    [Fact]
    public async Task A_disabled_gradient_keeps_its_stops()
    {
        var log = new Log();
        var cut = Render<BzColorPicker>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.ShowGradient, true)
            .Add(x => x.Value, "linear-gradient(90deg, #ff0000 0%, #00ff00 50%, #0000ff 100%)")
            .Add(x => x.ValueChanged, (string value) => log.Values.Add(value)));

        Assert.True(cut.Find("[data-slot=color-gradient-add]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-slot=color-gradient-remove]").HasAttribute("disabled"));
        Assert.True(cut.Find("input[data-slot=color-gradient-angle]").HasAttribute("disabled"));
        cut.FindAll("[data-slot=color-gradient-thumb]")[1].KeyDown("Delete");
        await Slide(cut, StopBar, 10, thumb: 1);

        Assert.Empty(log.Values);
        Assert.Equal(3, cut.FindAll("[data-slot=color-gradient-thumb]").Count);
    }

    // ---- the frame ----------------------------------------------------------------------------

    [Theory]
    [InlineData(ColorPickerSize.Sm, "sm")]
    [InlineData(ColorPickerSize.Default, "md")]
    [InlineData(ColorPickerSize.Lg, "lg")]
    public void Each_size_is_named_on_the_root(ColorPickerSize size, string name)
    {
        var root = Render<BzColorPicker>(p => p.Add(x => x.Size, size)).Find("[data-slot=color-picker]");

        Assert.Equal(name, root.GetAttribute("data-size"));
        Assert.Contains($"bz-color-picker-size-{name}", root.ClassList);
    }

    [Fact]
    public void The_card_frame_can_be_dropped_and_class_and_attributes_reach_the_root()
    {
        var framed = Render<BzColorPicker>().Find("[data-slot=color-picker]");
        Assert.Contains("bz-color-picker-bordered", framed.ClassList);
        Assert.False(framed.HasAttribute("style"));

        var bare = Render<BzColorPicker>(p => p
            .Add(x => x.Bordered, false)
            .Add(x => x.Class, "w-80")
            .AddUnmatched("id", "brand-color")).Find("[data-slot=color-picker]");
        Assert.DoesNotContain("bz-color-picker-bordered", bare.ClassList);
        Assert.Contains("w-80", bare.ClassList);
        Assert.Equal("brand-color", bare.Id);
    }

    [Fact]
    public void Cursors_are_published_for_every_draggable_part()
    {
        var root = Render<BzColorPicker>(p => p
            .Add(x => x.Cursor, "crosshair")
            .Add(x => x.GrabbingCursor, "move")).Find("[data-slot=color-picker]");

        Assert.Equal("--bz-color-cursor-stop:crosshair;--bz-color-cursor-grab:move;", root.GetAttribute("style"));
    }

    [Fact]
    public void A_custom_layout_renders_only_the_parts_it_names()
    {
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p
            .Add(x => x.ShowAlpha, true)
            .AddChildContent<BzColorPreview>()
            .AddChildContent<BzColorInput>(i => i.Add(x => x.Format, ColorFormat.Rgb).Add(x => x.Channels, false))
            .AddChildContent<BzColorSwatches>(s => s.AddChildContent<BzColorSwatch>(
                one => one.Add(x => x.Color, "#00ff0080").Add(x => x.AriaLabel, "Brand green"))));

        Assert.Empty(cut.FindAll("[data-slot=color-area], [data-color-slider]"));
        var preview = cut.Find("[data-slot=color-preview]");
        Assert.Equal("true", preview.GetAttribute("aria-hidden"));
        Assert.Contains("rgb(255 0 0)", preview.GetAttribute("style"));
        // A part can show another format than the one the picker writes.
        Assert.Equal("rgb(255 0 0)", Shown(cut));
        Assert.Equal("Brand green", cut.Find("[data-slot=color-swatch]").GetAttribute("aria-label"));

        cut.Find("[data-slot=color-swatch]").Click();

        Assert.Equal(["#00ff0080"], log.Values);
        Assert.Contains("rgb(0 255 0 / 0.5)", cut.Find("[data-slot=color-preview]").GetAttribute("style"));
        Assert.Equal("rgb(0 255 0 / 0.5)", Shown(cut));
    }

    public static TheoryData<Type> Parts =>
    [
        typeof(BzColorArea), typeof(BzColorHueSlider), typeof(BzColorAlphaSlider), typeof(BzColorInput),
        typeof(BzColorOpacityInput), typeof(BzColorFormatSelect), typeof(BzColorSwatch), typeof(BzColorSaved),
        typeof(BzColorPalette), typeof(BzColorPaletteGroup), typeof(BzColorPreview), typeof(BzColorModeTabs),
        typeof(BzColorGradientBar), typeof(BzColorGradientType), typeof(BzColorImage), typeof(BzColorEyeDropper),
    ];

    [Theory]
    [MemberData(nameof(Parts))]
    public void A_part_outside_a_picker_says_where_it_belongs(Type part)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render(builder =>
        {
            builder.OpenComponent(0, part);
            builder.CloseComponent();
        }));

        Assert.Equal($"{part.Name} must be placed inside a BzColorPicker.", error.Message);
    }

    // ---- the eye dropper ----------------------------------------------------------------------

    [Fact]
    public void The_eye_dropper_is_not_rendered_where_the_browser_has_none()
    {
        var cut = Render<BzColorPicker>(p => p.Add(x => x.ShowEyeDropper, true));

        Assert.Empty(cut.FindAll("[data-slot=color-eyedropper]"));
    }

    [Fact]
    public void The_eye_dropper_applies_the_picked_pixel_and_keeps_the_alpha()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/colorArea.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<bool>("supportsEyeDropper").SetResult(true);
        var pick = module.Setup<string?>("openEyeDropper");
        var log = new Log();
        var cut = Picker(log, "#ff000080", p => p.Add(x => x.ShowEyeDropper, true).Add(x => x.ShowAlpha, true));

        var button = cut.Find("[data-slot=color-eyedropper]");
        Assert.Equal("Pick color from screen", button.GetAttribute("aria-label"));
        button.Click();
        Assert.Empty(log.Values);

        pick.SetResult("#00ff00");

        cut.WaitForAssertion(() => Assert.Equal(["#00ff0080"], log.Values));
        Assert.Equal(["#00ff0080"], log.Commits);
    }

    [Fact]
    public void A_dismissed_eye_dropper_changes_nothing()
    {
        var module = JSInterop.SetupModule("./_content/blaizio.base/dist/colorArea.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<bool>("supportsEyeDropper").SetResult(true);
        module.Setup<string?>("openEyeDropper").SetResult(null);
        var log = new Log();
        var cut = Picker(log, "#ff0000", p => p.Add(x => x.ShowEyeDropper, true));

        cut.Find("[data-slot=color-eyedropper]").Click();

        Assert.Empty(log.Values);
        Assert.Empty(log.Commits);
    }
}
