using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The headless message scroller root: the element it renders, the options it starts the
/// transcript engine with, and the methods that drive the engine from code. Following, anchoring
/// and the scroll buttons are ts/messageScroller.ts and are exercised in a browser; here the
/// engine is a recording stand-in.
/// </summary>
public class MessageScrollerRenderTests : BunitContext
{
    private const string ModulePath = "./_content/blaizio.base/dist/messageScroller.js";

    private (BunitJSModuleInterop Module, BunitJSModuleInterop Engine) SetupEngine()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        var engine = module.SetupModule(invocation => invocation.Identifier == "createMessageScroller");
        engine.Mode = JSRuntimeMode.Loose;
        return (module, engine);
    }

    [Fact]
    public void Renders_the_root_with_its_slot_content_class_and_attributes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<BaseMessageScroller>(p => p
            .Add(x => x.Class, "h-96")
            .Add(x => x.Style, "contain:strict")
            .AddUnmatched("aria-label", "Conversation")
            .AddChildContent("<p>hello</p>"));

        var root = cut.Find("div[data-slot=message-scroller]");
        Assert.Equal("h-96", root.GetAttribute("class"));
        Assert.Equal("contain:strict", root.GetAttribute("style"));
        Assert.Equal("Conversation", root.GetAttribute("aria-label"));
        Assert.Equal("hello", root.QuerySelector("p")!.TextContent);
    }

    [Fact]
    public void The_engine_starts_once_with_the_defaults()
    {
        var (module, _) = SetupEngine();

        var cut = Render<BaseMessageScroller>();
        cut.Render();

        var create = Assert.Single(module.Invocations, i => i.Identifier == "createMessageScroller");
        Assert.Equal(
            """{"autoScroll":true,"defaultPosition":"end","anchorPeek":16,"followMargin":48}""",
            JsonSerializer.Serialize(create.Arguments[1], JsonSerializerOptions.Web));
    }

    [Fact]
    public void The_engine_starts_with_the_options_that_were_set()
    {
        var (module, _) = SetupEngine();

        Render<BaseMessageScroller>(p => p
            .Add(x => x.AutoScroll, false)
            .Add(x => x.DefaultPosition, MessageScrollerEdge.Start)
            .Add(x => x.AnchorPeek, 0)
            .Add(x => x.FollowMargin, 120));

        var create = Assert.Single(module.Invocations, i => i.Identifier == "createMessageScroller");
        Assert.Equal(
            """{"autoScroll":false,"defaultPosition":"start","anchorPeek":0,"followMargin":120}""",
            JsonSerializer.Serialize(create.Arguments[1], JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Each_method_drives_the_engine()
    {
        var (_, engine) = SetupEngine();
        var cut = Render<BaseMessageScroller>();

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.ScrollToEndAsync();
            await cut.Instance.ScrollToStartAsync();
            await cut.Instance.ScrollToMessageAsync("m-42");
            await cut.Instance.ReleaseReservedSpaceAsync();
        });

        Assert.Equal(
            ["scrollToEnd", "scrollToStart", "scrollToMessage", "releaseReservedSpace"],
            engine.Invocations.Select(i => i.Identifier));
        var toMessage = engine.Invocations.Single(i => i.Identifier == "scrollToMessage");
        Assert.Equal("m-42", Assert.Single(toMessage.Arguments));
        Assert.Empty(engine.Invocations.Single(i => i.Identifier == "scrollToEnd").Arguments);
    }

    [Fact]
    public async Task A_method_called_while_there_is_no_engine_does_nothing()
    {
        // The factory answers with nothing when its element is not in the document yet.
        var module = new RecordingJsModule();
        Services.AddSingleton<IJSRuntime>(new RecordingJsRuntime(module));
        var cut = Render<BaseMessageScroller>();

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.ScrollToEndAsync();
            await cut.Instance.ScrollToMessageAsync("m-1");
        });

        Assert.Equal(["createMessageScroller"], module.Identifiers);
    }

    [Fact]
    public async Task A_method_called_after_the_connection_is_gone_is_quiet()
    {
        var (_, engine) = SetupEngine();
        engine.SetupVoid("scrollToEnd", _ => true).SetException(new JSDisconnectedException("circuit gone"));
        var cut = Render<BaseMessageScroller>();

        await cut.InvokeAsync(() => cut.Instance.ScrollToEndAsync().AsTask());

        Assert.Single(engine.Invocations, i => i.Identifier == "scrollToEnd");
    }

    [Fact]
    public void A_connection_lost_before_the_engine_started_does_not_break_the_render()
    {
        Services.AddSingleton<IJSRuntime>(new RecordingJsRuntime(
            _ => Task.FromException<IJSObjectReference>(new JSDisconnectedException("circuit gone"))));

        var cut = Render<BaseMessageScroller>(p => p.AddChildContent("transcript"));

        Assert.Equal("transcript", cut.Find("[data-slot=message-scroller]").TextContent);
    }

    [Fact]
    public async Task Leaving_the_page_releases_the_engine()
    {
        var (_, engine) = SetupEngine();
        Render<BaseMessageScroller>();

        await DisposeComponentsAsync();

        Assert.Single(engine.Invocations, i => i.Identifier == "dispose");
    }
}
