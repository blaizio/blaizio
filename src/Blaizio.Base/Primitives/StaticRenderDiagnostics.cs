using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Blaizio;

/// <summary>
/// Development aid for the page that "silently does nothing": a component that needs events was
/// rendered on a statically rendered page with no render mode assigned, so it will never respond.
/// </summary>
/// <remarks>
/// <para>
/// The warning goes to the server log (category <see cref="Category"/>), once per component type
/// for the lifetime of the app. A browser console message is not an option: a static page may load
/// no script at all, and the code that notices runs on the server anyway.
/// </para>
/// <para>
/// It is on while the app runs in the Development environment, read from the
/// <c>ASPNETCORE_ENVIRONMENT</c> / <c>DOTNET_ENVIRONMENT</c> variables the host itself reads (the
/// hosting abstractions are not a dependency of this package). To silence it there, set the log
/// level of the <see cref="Category"/> category to <c>None</c>.
/// </para>
/// </remarks>
internal sealed class StaticRenderDiagnostics
{
    /// <summary>The log category the warning is written under.</summary>
    public const string Category = "Blaizio.StaticRender";

    private readonly ConcurrentDictionary<Type, bool> _reported = new();
    private readonly ILogger? _logger;

    /// <summary>The instance <c>AddBlaizio()</c> registers: on in Development only.</summary>
    public StaticRenderDiagnostics(IServiceProvider services)
        : this(services.GetService(typeof(ILoggerFactory)) as ILoggerFactory, IsDevelopment())
    {
    }

    internal StaticRenderDiagnostics(ILoggerFactory? loggers, bool enabled)
    {
        _logger = enabled ? loggers?.CreateLogger(Category) : null;
    }

    /// <summary>Logs the warning for <paramref name="component"/> unless that type was reported before.</summary>
    public void Report(Type component)
    {
        if (_logger is null || !_reported.TryAdd(component, true)) return;

        _logger.LogWarning(
            "{Component} was rendered statically, with no interactive render mode, and will not " +
            "respond to input. Give the component, or the page it is on, an interactive render mode " +
            "(for example @rendermode InteractiveServer). Logged once per component type.",
            component.Name);
    }

    private static bool IsDevelopment()
    {
        var name = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                   ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(name, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
