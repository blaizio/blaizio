using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// The real static renderer (<see cref="HtmlRenderer"/>), not a test double: what a statically
/// rendered page gets. Covers the shared "is this render interactive" members of
/// <see cref="BzComponentBase"/> through the two components that branch on them, and the
/// development warning for a component that does nothing without events.
/// </summary>
public class StaticRenderTests
{
    private sealed class Capture : ILoggerFactory
    {
        public List<(string Category, LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        private sealed class Sink(Capture owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries) owner.Entries.Add((category, logLevel, formatter(state, exception)));
            }
        }
    }

    // A static page has no script runtime to call: any interop from a static render is a defect.
    private sealed class NoScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException($"Script call '{identifier}' during a static render.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException($"Script call '{identifier}' during a static render.");
    }

    private static ServiceProvider Services(Capture log, bool development = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(log);
        services.AddSingleton<IJSRuntime, NoScript>();
        // What AddBlaizio() registers, with the environment decided here instead of read.
        services.AddSingleton(new StaticRenderDiagnostics(log, development));
        services.AddBlaizio();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync<T>(IServiceProvider services, Dictionary<string, object?>? parameters = null)
        where T : IComponent
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var view = parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters);
            var root = await renderer.RenderComponentAsync<T>(view);
            return root.ToHtmlString();
        });
    }

    private static List<string> Warnings(Capture log) =>
        log.Entries.Where(e => e.Category == StaticRenderDiagnostics.Category && e.Level == LogLevel.Warning)
            .Select(e => e.Message).ToList();

    [Fact]
    public async Task A_component_that_needs_events_is_reported_once_per_type()
    {
        var log = new Capture();
        await using var services = Services(log);

        var html = await RenderAsync<BaseSwitch>(services);
        await RenderAsync<BaseSwitch>(services);
        await RenderAsync<BaseSwitch>(services);

        // It still renders: the warning is a hint, never a failure.
        Assert.Contains("role=\"switch\"", html);
        var warning = Assert.Single(Warnings(log));
        Assert.Contains(nameof(BaseSwitch), warning);
        Assert.Contains("@rendermode", warning);
    }

    [Fact]
    public async Task Each_component_type_gets_its_own_report()
    {
        var log = new Capture();
        await using var services = Services(log);

        await RenderAsync<BaseSwitch>(services);
        await RenderAsync<BaseCheckbox>(services);

        Assert.Equal(2, Warnings(log).Count);
    }

    [Fact]
    public async Task A_component_that_works_without_events_is_not_reported()
    {
        var log = new Capture();
        await using var services = Services(log);

        await RenderAsync<BaseSeparator>(services);
        await RenderAsync<BaseProgress>(services);

        Assert.Empty(Warnings(log));
    }

    [Fact]
    public async Task Outside_development_nothing_is_reported()
    {
        var log = new Capture();
        await using var services = Services(log, development: false);

        await RenderAsync<BaseSwitch>(services);

        Assert.Empty(Warnings(log));
    }

    [Fact]
    public async Task Without_the_registration_a_static_render_still_works()
    {
        // A host that never called AddBlaizio(): no diagnostics service, no warning, no throw.
        var log = new Capture();
        var bare = new ServiceCollection();
        bare.AddSingleton<ILoggerFactory>(log);
        bare.AddSingleton<IJSRuntime, NoScript>();
        await using var services = bare.BuildServiceProvider();

        var html = await RenderAsync<BaseSwitch>(services);

        Assert.Contains("role=\"switch\"", html);
        Assert.Empty(Warnings(log));
    }

    [Fact]
    public async Task The_registered_service_reads_the_environment_itself()
    {
        // Only AddBlaizio(): the service is built from the container. The test process is not a
        // Development host, so it stays silent; what is checked is that construction works.
        var log = new Capture();
        var plain = new ServiceCollection();
        plain.AddSingleton<ILoggerFactory>(log);
        plain.AddSingleton<IJSRuntime, NoScript>();
        plain.AddBlaizio();
        await using var services = plain.BuildServiceProvider();

        Assert.NotNull(services.GetService<StaticRenderDiagnostics>());
        await RenderAsync<BaseSwitch>(services);
    }

    private static RenderFragment AvatarParts => builder =>
    {
        builder.OpenComponent<BaseAvatarImage>(0);
        builder.AddAttribute(1, nameof(BaseAvatarImage.Src), "/me.png");
        builder.AddAttribute(2, nameof(BaseAvatarImage.Alt), "Me");
        builder.CloseComponent();
        builder.OpenComponent<BaseAvatarFallback>(3);
        builder.AddAttribute(4, nameof(BaseAvatarFallback.ChildContent), (RenderFragment)(b => b.AddContent(0, "ME")));
        builder.CloseComponent();
    };

    [Fact]
    public async Task On_a_static_page_the_avatar_image_swaps_itself_in()
    {
        var log = new Capture();
        await using var services = Services(log);

        var html = await RenderAsync<BaseAvatar>(services, new() { [nameof(BaseAvatar.ChildContent)] = AvatarParts });

        // No Blazor event will ever report the load there: the element carries its own handler.
        Assert.Contains("onload=\"", html);
        Assert.Empty(Warnings(log));
    }

    private static RenderFragment ImageParts => builder =>
    {
        builder.OpenComponent<BaseImageImg>(0);
        builder.AddAttribute(1, nameof(BaseImageImg.Src), "/hero.jpg");
        builder.AddAttribute(2, nameof(BaseImageImg.Alt), "Hero");
        builder.CloseComponent();
    };

    [Fact]
    public async Task On_a_static_page_the_image_counts_as_loaded()
    {
        var log = new Capture();
        await using var services = Services(log);

        var html = await RenderAsync<BaseImage>(services, new() { [nameof(BaseImage.ChildContent)] = ImageParts });

        Assert.Contains("data-state=\"loaded\"", html);
        Assert.DoesNotContain("data-state=\"loading\"", html);
    }
}
