using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

public class PresetCodeTests
{
    [Fact]
    public void Default_selection_encodes_to_00()
    {
        Assert.Equal("00", PresetCode.Encode(new PresetSelection("ember", "nova", Rtl: false)));
    }

    [Fact]
    public void Style_preset_rtl_round_trips_as_v1()
    {
        var code = PresetCode.Encode(new PresetSelection("forge", "quasar", Rtl: true));
        Assert.Equal("32r", code);

        Assert.True(PresetCode.TryDecode(code, out var s));
        Assert.Equal(new PresetSelection("forge", "quasar", Rtl: true), s);
    }

    [Fact]
    public void Overlays_round_trip_as_v2()
    {
        var selection = new PresetSelection("aura", "nebula", Rtl: true,
            Chart: "sunset", Heading: "classic", Font: "soft", Radius: "xl");
        var code = PresetCode.Encode(selection);
        Assert.Equal("512244r", code);

        Assert.True(PresetCode.TryDecode(code, out var decoded));
        Assert.Equal(selection, decoded);
    }

    [Fact]
    public void Any_default_overlay_still_emits_v2_when_one_differs()
    {
        var code = PresetCode.Encode(new PresetSelection("ember", "nova", Rtl: false, Radius: "lg"));
        Assert.Equal("000003", code);

        Assert.True(PresetCode.TryDecode(code, out var s));
        Assert.Equal("lg", s.Radius);
        Assert.Equal("default", s.Chart);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]           // too short
    [InlineData("0000")]        // dead length between v1 and v2
    [InlineData("00000000")]    // too long
    [InlineData("0x")]          // preset index out of range
    [InlineData("00z")]         // v1 suffix must be 'r'
    [InlineData("000000x")]     // v2 suffix must be 'r'
    [InlineData("0f")]          // preset index 15 out of range (15 presets, 0-e)
    [InlineData("nova")]        // a preset NAME is not a code (dead length)
    public void Malformed_codes_are_rejected(string? code)
    {
        Assert.False(PresetCode.TryDecode(code, out _));
    }

    [Fact]
    public void Decode_is_case_and_whitespace_tolerant()
    {
        Assert.True(PresetCode.TryDecode(" 32R ", out var s));
        Assert.Equal(new PresetSelection("forge", "quasar", Rtl: true), s);
    }

    [Fact]
    public void Every_v1_combination_round_trips()
    {
        foreach (var style in PresetCode.Styles)
        foreach (var preset in PresetCode.Presets)
        foreach (var rtl in new[] { false, true })
        {
            var selection = new PresetSelection(style, preset, rtl);
            Assert.True(PresetCode.TryDecode(PresetCode.Encode(selection), out var decoded));
            Assert.Equal(selection, decoded);
        }
    }
}
