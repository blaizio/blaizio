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
    [InlineData("0g")]          // preset index 16 out of range (16 presets, 0-f)
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

    [Fact]
    public void Token_overrides_round_trip_as_v3()
    {
        var selection = new PresetSelection("ember", "zenith", Rtl: false)
        {
            Overrides =
            [
                new TokenOverride("primary", Dark: false, new OklchColor(0.55, 0.22, 304)),
                new TokenOverride("primary", Dark: true, new OklchColor(0.74, 0.14, 155)),
                new TokenOverride("chart-5", Dark: true, new OklchColor(0.7, 0.13, 195)),
            ],
        };
        var code = PresetCode.Encode(selection);
        Assert.Contains('-', code);

        Assert.True(PresetCode.TryDecode(code, out var decoded));
        Assert.Equal(selection, decoded);
    }

    [Fact]
    public void V3_quantizes_within_one_step()
    {
        var color = new OklchColor(0.5185, 0.2237, 304.42);
        var selection = new PresetSelection("ember", "nova", Rtl: true)
        {
            Overrides = [new TokenOverride("accent", Dark: true, color)],
        };
        Assert.True(PresetCode.TryDecode(PresetCode.Encode(selection), out var decoded));
        var round = decoded.Overrides[0].Color;
        Assert.Equal(color.L, round.L, 3);
        Assert.Equal(color.C, round.C, 3);
        Assert.Equal(304, round.H, 0);
        Assert.True(decoded.Overrides[0].Dark);
        Assert.Equal("accent", decoded.Overrides[0].Token);
    }

    [Theory]
    [InlineData("00-")]           // empty override tail
    [InlineData("00-0000000")]    // tail not a multiple of 8
    [InlineData("00-zz000000")]   // token slot 1295 out of range (tokens x 2 modes)
    [InlineData("00-00zz0000")]   // L over 1000
    [InlineData("00-000000zz")]   // H over 359
    public void Malformed_v3_codes_are_rejected(string code)
    {
        Assert.False(PresetCode.TryDecode(code, out _));
    }

    [Fact]
    public void Every_token_and_mode_round_trips()
    {
        foreach (var token in ThemeTokens.All)
        foreach (var dark in new[] { false, true })
        {
            var selection = new PresetSelection("ember", "nova", Rtl: false)
            {
                Overrides = [new TokenOverride(token, dark, new OklchColor(0.5, 0.1, 200))],
            };
            Assert.True(PresetCode.TryDecode(PresetCode.Encode(selection), out var decoded));
            Assert.Equal(selection, decoded);
        }
    }

    [Fact]
    public void Overrides_do_not_change_the_base_code()
    {
        var plain = PresetCode.Encode(new PresetSelection("forge", "quasar", Rtl: true));
        var edited = PresetCode.Encode(new PresetSelection("forge", "quasar", Rtl: true)
        {
            Overrides = [new TokenOverride("primary", false, new OklchColor(0.5, 0.13, 155))],
        });
        Assert.StartsWith(plain + "-", edited);
    }
}
