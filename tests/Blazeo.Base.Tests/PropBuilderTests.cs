using Blazeo;
using Xunit;

namespace Blazeo.Base.Tests;

/// <summary>Pure (no-render) tests for the attribute assembly + merge precedence rules.</summary>
public class PropBuilderTests
{
    [Fact]
    public void Flag_is_present_when_true_and_omitted_when_false()
    {
        var on = new PropBuilder().Flag("disabled", true).Merge(null);
        var off = new PropBuilder().Flag("disabled", false).Merge(null);

        Assert.Equal("", on["disabled"]);
        Assert.False(off.ContainsKey("disabled"));
    }

    [Fact]
    public void Aria_bool_emits_literal_true_false_strings()
    {
        var dict = new PropBuilder().Aria("checked", true).Aria("expanded", false).Merge(null);

        Assert.Equal("true", dict["aria-checked"]);
        Assert.Equal("false", dict["aria-expanded"]);
    }

    [Fact]
    public void Class_concatenates_component_then_param_then_splatted()
    {
        var dict = new PropBuilder()
            .Set("class", "base")
            .Merge(new Dictionary<string, object> { ["class"] = "extra" }, userClass: "fromparam");

        Assert.Equal("base fromparam extra", dict["class"]);
    }

    [Fact]
    public void Style_concatenates_with_semicolon()
    {
        var dict = new PropBuilder()
            .Set("style", "color:red")
            .Merge(null, userStyle: "margin:0");

        Assert.Equal("color:red; margin:0", dict["style"]);
    }

    [Fact]
    public void Consumer_value_wins_on_non_event_conflict()
    {
        var dict = new PropBuilder()
            .Set("role", "none")
            .Merge(new Dictionary<string, object> { ["role"] = "separator" });

        Assert.Equal("separator", dict["role"]);
    }

    [Fact]
    public void Null_user_attributes_returns_assembled_props_unchanged()
    {
        var dict = new PropBuilder().Set("id", "a").Merge(null);

        Assert.Single(dict);
        Assert.Equal("a", dict["id"]);
    }
}
