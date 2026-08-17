using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio;

/// <inheritdoc cref="ICore"/>
public sealed class Core(IJSRuntime js) : ICore
{
    private Task<IJSObjectReference> Module() =>
        JsModules.ImportAsync(js, "./_content/blaizio.base/dist/core.js");

    /// <inheritdoc/>
    public async ValueTask<bool> FocusAsync(ElementReference element, FocusOptions? options = null) =>
        await (await Module()).InvokeAsync<bool>("focusElement", element, options);

    /// <inheritdoc/>
    public async ValueTask EnsureGuardsAsync() =>
        // Importing the module installs the guard; the explicit call keeps this correct even if
        // that side effect ever moves.
        await (await Module()).InvokeVoidAsync("installGuards");
}
