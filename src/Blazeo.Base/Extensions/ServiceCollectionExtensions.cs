using Blazeo;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the headless Blazeo primitives.</summary>
public static class BlazeoBaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services the headless layer needs - currently the scoped <see cref="IDialogStore"/>
    /// behind <see cref="BzDialogHost"/> and the imperative dialog API. Scoped means per-circuit on
    /// Blazor Server and per-app on WebAssembly. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBlazeoBase(this IServiceCollection services)
    {
        services.TryAddScoped<IDialogStore, DialogStore>();
        return services;
    }
}
