using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The shared JS module cache (JsModules): floating surfaces used to import the same
/// presence/positioning/dismiss modules once per open instance - one interop round-trip and one
/// proxy each. These prove one import per module path per runtime, however many components share
/// the circuit (PERF-02, audit batch 8).
/// </summary>
public class JsModulesTests : BunitContext
{
    public JsModulesTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static void AddPopover(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int seq, int n)
    {
        builder.OpenRegion(seq);
        builder.OpenComponent<BasePopover>(0);
        builder.AddComponentParameter(1, nameof(BasePopover.Open), (bool?)true);
        builder.AddComponentParameter(2, nameof(BasePopover.ChildContent), (RenderFragment)(p =>
        {
            p.OpenComponent<BasePopoverTrigger>(0);
            p.AddComponentParameter(1, nameof(BasePopoverTrigger.ChildContent),
                (RenderFragment)(t => t.AddContent(0, $"Trigger {n}")));
            p.CloseComponent();
            p.OpenComponent<BasePopoverContent>(2);
            p.AddComponentParameter(3, nameof(BasePopoverContent.ChildContent),
                (RenderFragment)(c => c.AddContent(0, $"Body {n}")));
            p.CloseComponent();
        }));
        builder.CloseComponent();
        builder.CloseRegion();
    }

    [Fact]
    public void Many_open_surfaces_import_each_module_once()
    {
        // Three open popovers: three roots (each warming positioning/presence/dismiss) + three
        // contents (each creating its instances from those modules). Without the cache that was up
        // to 18 import interop calls; with it, each distinct module path is imported exactly once.
        Render(b =>
        {
            AddPopover(b, 0, 1);
            AddPopover(b, 1, 2);
            AddPopover(b, 2, 3);
        });

        var imports = JSInterop.Invocations
            .Where(i => i.Identifier == "import")
            .Select(i => (string?)i.Arguments[0])
            .ToList();

        Assert.NotEmpty(imports);
        Assert.Equal(imports.Distinct(StringComparer.Ordinal).Count(), imports.Count);
    }
}
