using Blaizio.Cli.Core.Styling;
using Xunit;

namespace Blaizio.Cli.Core.Tests;

/// <summary>
/// The "skin" to "style" rename kept the old Cli.Core surface as obsolete forwarders: an
/// <see cref="ICssAssetProvider"/> written against either set of names works through both, and
/// <see cref="SkinInliner"/> behaves exactly like the <see cref="StyleInliner"/> it wraps.
/// </summary>
#pragma warning disable CS0618 // the obsolete members are what these tests pin
public class StyleRenameCompatTests
{
    [Fact]
    public void A_provider_implementing_the_style_members_answers_the_obsolete_skin_ones()
    {
        ICssAssetProvider assets = new FakeCssAssets();

        Assert.Equal(assets.GetSharedStyleCss(), assets.GetSharedSkinCss());
        Assert.Equal(assets.GetStyleCss("ember"), assets.GetSkinCss("ember"));
        Assert.Equal(assets.AvailableStyles, assets.AvailableSkins);
    }

    [Fact]
    public void A_provider_implementing_only_the_obsolete_skin_members_answers_the_style_ones()
    {
        ICssAssetProvider assets = new LegacyAssets();

        Assert.Equal("/* shared */", assets.GetSharedStyleCss());
        Assert.Equal("/* legacy:spark */", assets.GetStyleCss("spark"));
        Assert.Equal(["spark"], assets.AvailableStyles);
    }

    [Fact]
    public void A_provider_implementing_neither_set_fails_clearly_instead_of_recursing()
    {
        ICssAssetProvider assets = new BareAssets();

        Assert.Throws<NotImplementedException>(() => assets.GetStyleCss("ember"));
        Assert.Throws<NotImplementedException>(() => assets.GetSkinCss("ember"));
        Assert.Throws<NotImplementedException>(() => assets.AvailableStyles);
    }

    [Fact]
    public void The_obsolete_skin_inliner_forwards_to_the_style_inliner()
    {
        const string shared = ".bz-button { @apply px-4 rounded-md; }";
        const string style = ".style-x { .bz-button { @apply rounded-none; } }";
        const string source = """<button class="bz-button">x</button>""";

        var legacy = SkinInliner.Create(shared, style);
        var current = StyleInliner.Create(shared, style);

        Assert.Equal(current.Tokens, legacy.Tokens);
        Assert.Equal(current.Resolve("bz-button"), legacy.Resolve("bz-button"));
        Assert.Equal(current.Inline(source), legacy.Inline(source));
    }

    private sealed class LegacyAssets : ICssAssetProvider
    {
        public string GetThemeCss() => "";
        public string GetAnimateCss() => "";
        public string GetBaseCss() => "";
        public string GetSharedSkinCss() => "/* shared */";
        public string GetSkinCss(string skin) => $"/* legacy:{skin} */";
        public IReadOnlyList<string> AvailableSkins { get; } = ["spark"];
        public string GetPresetCss(string preset) => "";
        public IReadOnlyList<string> AvailablePresets { get; } = [];
    }

    private sealed class BareAssets : ICssAssetProvider
    {
        public string GetThemeCss() => "";
        public string GetAnimateCss() => "";
        public string GetBaseCss() => "";
        public string GetPresetCss(string preset) => "";
        public IReadOnlyList<string> AvailablePresets { get; } = [];
    }
}
#pragma warning restore CS0618
