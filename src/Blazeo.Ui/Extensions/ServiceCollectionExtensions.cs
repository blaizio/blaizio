using Blazeo.Ui;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration for the styled Blazeo.Ui services.</summary>
public static class BlazeoUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the styled Blazeo.Ui services and the headless layer they build on - currently the
    /// scoped <see cref="IDialogService"/> (and its <see cref="Blazeo.IDialogStore"/>) behind
    /// <c>&lt;DialogHost /&gt;</c>. Scoped means per-circuit on Blazor Server and per-app on WebAssembly.
    /// Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBlazeoUi(this IServiceCollection services)
    {
        services.AddBlazeoBase();
        services.TryAddScoped<IDialogService, DialogService>();
        return services;
    }
}
