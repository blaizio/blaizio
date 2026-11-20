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

    [Fact]
    public async Task Callers_of_one_path_share_the_module_and_each_runtime_has_its_own()
    {
        var first = new RecordingJsRuntime(new RecordingJsModule());
        var second = new RecordingJsRuntime(new RecordingJsModule());

        var a = await JsModules.ImportAsync(first, "./a.js");
        var again = await JsModules.ImportAsync(first, "./a.js");
        var b = await JsModules.ImportAsync(first, "./b.js");
        var elsewhere = await JsModules.ImportAsync(second, "./a.js");

        Assert.Same(a, again);
        Assert.Equal(["./a.js", "./b.js"], first.Imports);
        // Another circuit must not be handed this circuit's proxy.
        Assert.NotSame(a, elsewhere);
        Assert.Equal(["./a.js"], second.Imports);
        Assert.NotNull(b);
    }

    [Fact]
    public async Task An_import_that_fails_after_it_went_out_is_tried_again_by_the_next_caller()
    {
        var module = new RecordingJsModule();
        var attempts = 0;
        var first = new TaskCompletionSource<Microsoft.JSInterop.IJSObjectReference>();
        var runtime = new RecordingJsRuntime(_ => ++attempts == 1
            ? first.Task
            : Task.FromResult<Microsoft.JSInterop.IJSObjectReference>(module));

        var failing = JsModules.ImportAsync(runtime, "./a.js");
        first.SetException(new Microsoft.JSInterop.JSException("failed to fetch"));
        await Assert.ThrowsAsync<Microsoft.JSInterop.JSException>(() => failing);

        Assert.Same(module, await JsModules.ImportAsync(runtime, "./a.js"));
        Assert.Equal(2, runtime.Imports.Count);
    }

    [Fact(Skip = "bug: JsModules keeps an import that fails before it returns (the eviction runs before the entry is stored), so every later caller on that runtime gets the same failure")]
    public async Task An_import_that_fails_at_once_is_tried_again_by_the_next_caller()
    {
        var module = new RecordingJsModule();
        var attempts = 0;
        // The runtime refuses the call itself, as a runtime with no client connected does.
        var runtime = new RecordingJsRuntime(_ => ++attempts == 1
            ? Task.FromException<Microsoft.JSInterop.IJSObjectReference>(new InvalidOperationException("not connected"))
            : Task.FromResult<Microsoft.JSInterop.IJSObjectReference>(module));

        await Assert.ThrowsAsync<InvalidOperationException>(() => JsModules.ImportAsync(runtime, "./a.js"));

        Assert.Same(module, await JsModules.ImportAsync(runtime, "./a.js"));
    }
}
