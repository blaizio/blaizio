using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// What keeps Blaizio.Base safe to trim, checked on the assembly itself. The trim analyzer does
/// not see any of this: an object handed to script is serialized by reflection over its runtime
/// type, and a script callback is found by name. A miss fails silently in a trimmed app only, so
/// the rules are enforced here.
/// </summary>
public class TrimSafetyTests
{
    private static readonly Assembly Base = typeof(BzComponentBase).Assembly;

    // The serializer settings script interop uses: camel-cased names.
    private static readonly JsonSerializerOptions Interop = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static IEnumerable<Type> OptionTypes() =>
        Base.GetTypes().Where(t => t is { IsAbstract: false, BaseType.IsGenericType: true }
                                   && t.BaseType.GetGenericTypeDefinition() == typeof(JsOptions<>));

    [Fact]
    public void No_anonymous_object_is_built_in_the_assembly()
    {
        // new { ... } compiles to a generated type whose property getters nothing calls. Trimming
        // removes them and the script receives an empty object. Use a JsOptions<T> type instead.
        var anonymous = Base.GetTypes()
            .Where(t => t.Name.Contains("AnonymousType") && t.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Select(t => t.Name);

        Assert.Empty(anonymous);
    }

    [Fact]
    public void Every_options_type_names_itself_as_the_kept_type()
    {
        var types = OptionTypes().ToList();
        Assert.NotEmpty(types);

        foreach (var type in types)
        {
            // JsOptions<TSelf> keeps the public properties of TSelf: it has to be the type itself.
            Assert.Equal(type, type.BaseType!.GetGenericArguments()[0]);
            Assert.True(type.IsSealed, $"{type.Name} must be sealed: a derived type's properties would not be kept.");
        }
    }

    // The property names each script reads, as written in its options interface (lib/ts).
    public static TheoryData<Type, string[]> ScriptContracts => new()
    {
        { typeof(PositioningOptions), ["side", "align", "sideOffset", "alignOffset", "collisionPadding", "inline"] },
        { typeof(DismissableLayerOptions), ["anchorSelector", "dismissOnEscape", "dismissOnOutsidePress", "dismissOnScroll"] },
        { typeof(MenuOptions), ["initialFocus", "typeaheadTimeout", "loop", "isSubmenu", "triggerSelector"] },
        { typeof(FocusScopeOptions), ["loop", "trapped", "hasMountAutoFocus", "hasUnmountAutoFocus", "passive"] },
        { typeof(RovingFocusOptions), ["orientation", "dir", "loop", "selectOnFocus"] },
        { typeof(CarouselOptions), ["horizontal", "loop", "autoplayMs"] },
        { typeof(ComboboxListOptions), ["inputId", "loop", "autoHighlight"] },
        { typeof(CommandListOptions), ["inputId", "loop"] },
        { typeof(NumericGuardOptions), ["decimal", "negative", "separator"] },
        { typeof(InputTagsOptions), ["delimiters"] },
        { typeof(MessageScrollerOptions), ["autoScroll", "defaultPosition", "anchorPeek", "followMargin"] },
        {
            typeof(SortableOptions),
            [
                "id", "strategy", "orientation", "group", "animationMs", "disabled", "sort", "transferOut",
                "transferIn", "delayMs", "lockAxis", "swapMode", "placeholder", "placeholderClass", "animation",
            ]
        },
        {
            typeof(TreeOptions),
            [
                "id", "group", "disabled", "dragEnabled", "delayMs", "hoverExpandMs", "transferIn", "transferOut",
                "indicatorClass",
            ]
        },
        { typeof(TreeUpdateOptions), ["disabled", "dragEnabled"] },
    };

    [Theory]
    [MemberData(nameof(ScriptContracts))]
    public void An_options_type_reaches_the_script_under_the_names_it_reads(Type type, string[] expected)
    {
        var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => Interop.PropertyNamingPolicy!.ConvertName(p.Name));

        Assert.Equal(expected.Order(), names.Order());
    }

    [Fact]
    public void Every_options_type_has_a_script_contract_row()
    {
        var covered = ScriptContracts.Select(row => (Type)row[0]).ToHashSet();

        Assert.Empty(OptionTypes().Where(t => !covered.Contains(t)).Select(t => t.Name));
    }

    [Fact]
    public void Defaults_match_what_the_scripts_assumed_for_an_absent_property()
    {
        // These three were left out by most callers when the payloads were anonymous objects.
        var layer = JsonSerializer.SerializeToElement(new DismissableLayerOptions { DismissOnEscape = true }, Interop);
        Assert.True(layer.GetProperty("dismissOnOutsidePress").GetBoolean());
        Assert.False(layer.GetProperty("dismissOnScroll").GetBoolean());
        Assert.Equal(JsonValueKind.Null, layer.GetProperty("anchorSelector").ValueKind);

        var scope = JsonSerializer.SerializeToElement(new FocusScopeOptions(), Interop);
        Assert.False(scope.GetProperty("passive").GetBoolean());

        var place = JsonSerializer.SerializeToElement(new PositioningOptions { Side = "top", Align = "center" }, Interop);
        Assert.Equal(8, place.GetProperty("collisionPadding").GetDouble());
        Assert.Equal("top", place.GetProperty("side").GetString());

        var menu = JsonSerializer.SerializeToElement(new MenuOptions { InitialFocus = "first", TriggerSelector = "#t" }, Interop);
        Assert.Equal(1000, menu.GetProperty("typeaheadTimeout").GetInt32());
    }

    [Fact]
    public void Script_callbacks_are_declared_on_the_type_the_object_reference_is_made_with()
    {
        // DotNetObjectReference.Create(this) keeps the public methods of the STATIC type of "this".
        // A [JSInvokable] method that only a derived type declares is unknown to the trimmer and
        // gets removed: the script's call then fails in a trimmed app.
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var referenced = Base.GetTypes()
            .SelectMany(t => t.GetFields(all))
            .Select(f => f.FieldType)
            .Where(f => f.IsGenericType && f.GetGenericTypeDefinition() == typeof(DotNetObjectReference<>))
            .Select(f => f.GetGenericArguments()[0])
            .Distinct()
            .ToList();
        Assert.NotEmpty(referenced);

        var offenders =
            from holder in referenced
            from derived in Base.GetTypes()
            where derived != holder && holder.IsAssignableFrom(derived)
            from method in derived.GetMethods(all)
            where method.IsDefined(typeof(JSInvokableAttribute), false)
            select $"{derived.Name}.{method.Name} (reference is DotNetObjectReference<{holder.Name}>)";

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(typeof(BaseAccordionContent))]
    [InlineData(typeof(BaseCollapsibleContent))]
    [InlineData(typeof(BaseTreePanel))]
    public void A_collapsible_panel_answers_both_script_callbacks(Type panel)
    {
        // How script interop finds an instance method: public instance methods of the runtime
        // type, inherited ones included, carrying the attribute.
        var invokable = panel.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.IsDefined(typeof(JSInvokableAttribute), false))
            .ToDictionary(m => m.Name);

        Assert.Equal(typeof(CollapsiblePanelBase), invokable["OnCloseFinished"].DeclaringType);
        Assert.Equal(typeof(CollapsiblePanelBase), invokable["OnBeforeMatch"].DeclaringType);
    }

    [Fact]
    public void The_assembly_is_marked_trimmable()
    {
        var metadata = Base.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "IsTrimmable");

        Assert.Equal("True", metadata?.Value);
    }

    private sealed class Greeting : ComponentBase
    {
        [Parameter] public string? Name { get; set; }
        [Parameter] public int Count { get; set; }
        [CascadingParameter] public DialogInstance? Dialog { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
            builder.AddContent(0, $"{Name ?? "nobody"}:{Count}");
    }

    private sealed class Store : IDialogStore
    {
        public RenderFragment? Content { get; private set; }
        public IReadOnlyList<DialogInstance> Instances => [];
        public event Action? Changed { add { } remove { } }

        public Task<DialogResult> OpenAsync(Func<DialogInstance, RenderFragment> contentFactory, DialogOptions options)
        {
            Content = contentFactory(null!);
            return Task.FromResult(DialogResult.Cancel());
        }
    }

    [Fact]
    public async Task A_component_dialog_is_rendered_as_itself_with_its_parameters()
    {
        // The service opens the component by its own type (no DynamicComponent in between, whose
        // Type parameter the trimmer cannot follow) and hands it the dictionary entry by entry.
        var store = new Store();
        var service = new DialogService(store, new BlaizioOptions());

        await service.ShowAsync<Greeting>(new Dictionary<string, object?> { ["Name"] = "Ada", ["Count"] = 3 });

        using var context = new Bunit.BunitContext();
        var cut = context.Render(store.Content!);
        Assert.Equal("Ada:3", cut.Markup);
    }

    [Fact]
    public async Task A_null_parameter_value_is_left_out_and_no_parameters_is_fine()
    {
        var store = new Store();
        var service = new DialogService(store, new BlaizioOptions());
        using var context = new Bunit.BunitContext();

        await service.ShowAsync<Greeting>(new Dictionary<string, object?> { ["Name"] = null, ["Count"] = 2 });
        Assert.Equal("nobody:2", context.Render(store.Content!).Markup);

        await service.ShowAsync<Greeting>();
        Assert.Equal("nobody:0", context.Render(store.Content!).Markup);
    }
}
