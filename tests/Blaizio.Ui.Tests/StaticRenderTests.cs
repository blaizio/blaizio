using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// The docs examples rendered with no interactivity at all: <see cref="HtmlRenderer"/>, no script
/// runtime, no events, no second render. That is what a statically rendered page gets, and what a
/// visitor sees of an interactive page until its runtime has started. The examples are the docs
/// site's own files (linked into this project by the <c>BlaizioLinkDocsExamples</c> target, see the
/// project file), so every new example joins the run without being listed here.
/// </summary>
public class StaticRenderTests
{
    // Families whose whole job is to show something: there is nothing to press before content
    // appears, so the content has to be in the first markup. Key = examples folder, value = the
    // data-slot names (exact, or the prefix before "-") the family renders.
    private static readonly Dictionary<string, string[]> DisplayFamilies = new()
    {
        ["Alert"] = ["alert"],
        ["AspectRatio"] = ["aspect-ratio"],
        ["Avatar"] = ["avatar"],
        ["Badge"] = ["badge"],
        ["Breadcrumb"] = ["breadcrumb"],
        ["Bubble"] = ["bubble"],
        ["Card"] = ["card"],
        ["Chart"] = ["chart", "pie-chart", "radar-chart", "radial-bar-chart"],
        ["CodeBlock"] = ["code-block"],
        ["Empty"] = ["empty"],
        ["Image"] = ["image"],
        ["Item"] = ["item"],
        ["Kbd"] = ["kbd"],
        ["Label"] = ["label"],
        ["Marker"] = ["marker"],
        ["Message"] = ["message"],
        ["Pagination"] = ["pagination"],
        ["Progress"] = ["progress", "circular-progress"],
        ["QrCode"] = ["qr-code"],
        ["Skeleton"] = ["skeleton"],
        ["Spinner"] = ["spinner"],
        ["Table"] = ["table"],
        ["Trace"] = ["trace"],
    };

    private static readonly Lazy<ILookup<string, Type>> s_examples = new(() =>
        typeof(StaticRenderTests).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                        && t.Namespace is { } ns && ns.Contains(".DocsExamples."))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToLookup(t => t.Namespace![(t.Namespace!.LastIndexOf('.') + 1)..]));

    public static TheoryData<string> Families()
    {
        var data = new TheoryData<string>();
        foreach (var family in s_examples.Value.Select(g => g.Key).Order(StringComparer.Ordinal)) data.Add(family);
        return data;
    }

    public static TheoryData<string> Display()
    {
        var data = new TheoryData<string>();
        foreach (var family in DisplayFamilies.Keys.Order(StringComparer.Ordinal)) data.Add(family);
        return data;
    }

    [Fact]
    public void The_docs_examples_are_part_of_this_project()
    {
        // The link target matches files by content; a change that made it match nothing would
        // leave every theory below with no rows and a green run.
        var examples = s_examples.Value;
        Assert.True(examples.Count > 70, $"only {examples.Count} example folders were linked");
        Assert.True(examples.Sum(g => g.Count()) > 700, $"only {examples.Sum(g => g.Count())} examples were linked");
        Assert.All(DisplayFamilies.Keys, family => Assert.True(examples.Contains(family), $"no examples for {family}"));
    }

    // The rule the display theory judges with, on markup written for it: what it must catch (an
    // image left in its loading state stays transparent for good) and what it must let through.
    [Theory]
    [InlineData("""<img data-slot="part" data-state="loading" class="opacity-0 data-[state=loaded]:opacity-100">""", true)]
    [InlineData("""<img data-slot="part" data-state="loaded" class="opacity-0 data-[state=loaded]:opacity-100">""", false)]
    [InlineData("""<div data-slot="part" hidden></div>""", true)]
    [InlineData("""<div hidden><p data-slot="part">inside</p></div>""", true)]
    [InlineData("""<div data-state="closed" hidden><p data-slot="part">inside</p></div>""", false)]
    [InlineData("""<div data-state="unchecked"><svg data-slot="part" class="hidden group-data-[state=checked]:block"></svg></div>""", true)]
    [InlineData("""<div data-state="checked"><svg data-slot="part" class="hidden group-data-[state=checked]:block"></svg></div>""", false)]
    [InlineData("""<div data-slot="part" class="invisible"></div>""", true)]
    [InlineData("""<div data-slot="part" class="hidden md:block"></div>""", false)]
    [InlineData("""<div data-slot="part" class="opacity-0 group-hover:opacity-100"></div>""", false)]
    public void The_hiding_rule_reads_the_markup_as_a_page_without_scripts_would(string markup, bool hidden)
    {
        var part = new HtmlParser().ParseDocument($"<body>{markup}</body>").Body!.QuerySelector("[data-slot=part]")!;

        Assert.Equal(hidden, HiddenBy(part) is not null);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public async Task Examples_render_without_interactivity(string family)
    {
        var failures = new List<string>();
        foreach (var example in s_examples.Value[family])
        {
            var (html, error) = await RenderAsync(example);
            if (error is not null) failures.Add($"{example.Name}: {error}");
            else if (string.IsNullOrWhiteSpace(html)) failures.Add($"{example.Name}: rendered nothing");
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Theory]
    [MemberData(nameof(Display))]
    public async Task Display_examples_show_their_content_without_interactivity(string family)
    {
        var slots = DisplayFamilies[family];
        var parser = new HtmlParser();
        var failures = new List<string>();
        foreach (var example in s_examples.Value[family])
        {
            var (html, error) = await RenderAsync(example);
            if (error is not null) { failures.Add($"{example.Name}: {error}"); continue; }

            var document = parser.ParseDocument($"<body>{html}</body>");
            var parts = document.Body!.QuerySelectorAll("[data-slot]").Where(e => IsOf(e, slots)).ToList();
            if (parts.Count == 0)
            {
                // The family's part can sit in a surface that is closed for now (a key hint in a
                // tooltip): then the trigger is what the page shows, and that is enough here.
                if (document.Body.QuerySelector("[data-slot$='-trigger']") is null)
                    failures.Add($"{example.Name}: no element of the family in the markup");
                continue;
            }

            // Every part the family rendered must be on screen as rendered: a part that waits for
            // a script or an event to lose a hiding class never loses it here.
            foreach (var part in parts)
                if (HiddenBy(part) is { } reason)
                    failures.Add($"{example.Name}: <{part.LocalName} data-slot=\"{part.GetAttribute("data-slot")}\"> {reason}");
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    private static bool IsOf(IElement element, string[] slots)
    {
        var slot = element.GetAttribute("data-slot")!;
        return slots.Any(s => slot == s || slot.StartsWith(s + "-", StringComparison.Ordinal));
    }

    /// <summary>Why <paramref name="element"/> is not visible in the markup as it stands, or null
    /// when it is. A hiding class counts only when nothing in the same class list brings the
    /// element back for the state it (or the ancestor a group variant names) is in.</summary>
    private static string? HiddenBy(IElement element)
    {
        for (IElement? e = element; e is not null; e = e.ParentElement)
        {
            if (!e.HasAttribute("hidden")) continue;
            // A disclosure that starts closed is hidden on purpose, and says so.
            if (e.GetAttribute("data-state") == "closed") return null;
            return e == element ? "has the hidden attribute" : $"sits inside a hidden <{e.LocalName}>";
        }

        var tokens = (element.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var hiding in tokens.Where(t => t is "hidden" or "invisible" or "opacity-0"))
        {
            if (!tokens.Any(t => BringsBack(element, hiding, t)))
                return $"keeps the class \"{hiding}\" (state \"{element.GetAttribute("data-state")}\")";
        }
        return null;
    }

    private static bool BringsBack(IElement element, string hiding, string token)
    {
        var colon = token.LastIndexOf(':');
        if (colon < 0) return false;
        var (variant, utility) = (token[..colon], token[(colon + 1)..]);
        var undoes = hiding switch
        {
            "opacity-0" => utility.StartsWith("opacity-", StringComparison.Ordinal) && utility != "opacity-0",
            "invisible" => utility == "visible",
            _ => utility is "block" or "flex" or "inline-flex" or "inline-block" or "inline" or "grid" or "contents" or "table-cell",
        };
        if (!undoes) return false;

        // data-[state=x]: the element's own state decides; group-data-[state=x]/name: an ancestor's.
        var state = System.Text.RegularExpressions.Regex.Match(variant, @"data-\[state=([^\]]+)\]");
        if (!state.Success) return true; // a breakpoint, hover or focus variant: not tied to a script
        var wanted = state.Groups[1].Value;
        if (!variant.StartsWith("group-", StringComparison.Ordinal))
            return element.GetAttribute("data-state") == wanted;
        for (var e = element.ParentElement; e is not null; e = e.ParentElement)
            if (e.GetAttribute("data-state") == wanted) return true;
        return false;
    }

    /// <summary>One example through <see cref="HtmlRenderer"/> in its own service scope. A script
    /// call or a navigation is an error here: neither exists without interactivity.</summary>
    private static async Task<(string Html, string? Error)> RenderAsync(Type example)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlaizio();
        services.AddSingleton<IJSRuntime, NoScripts>();
        services.AddSingleton<NavigationManager, FixedNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        try
        {
            var render = renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync(example)).ToHtmlString());
            // HtmlRenderer waits for every pending task of the first render: an example that
            // loops on a timer in OnInitializedAsync would never come back.
            if (await Task.WhenAny(render, Task.Delay(TimeSpan.FromSeconds(15))) != render)
                return ("", "did not finish rendering within 15 s");
            return (await render, null);
        }
        catch (Exception e)
        {
            return ("", $"{e.GetType().Name}: {e.Message.Split('\n')[0].Trim()}");
        }
    }

    private sealed class NoScripts : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException($"script call '{identifier}' during static rendering");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException($"script call '{identifier}' during static rendering");
    }

    private sealed class FixedNavigation : NavigationManager
    {
        public FixedNavigation() => Initialize("https://example.test/", "https://example.test/docs");

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            throw new InvalidOperationException($"navigation to '{uri}' during static rendering");
    }
}
