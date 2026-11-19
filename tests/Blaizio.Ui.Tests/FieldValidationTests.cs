using System.Linq.Expressions;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// <c>BzField For</c> inside an <c>EditForm</c>: the field tells the form's EditContext when its
/// property's value changes, so the messages follow the edit instead of waiting for a submit. The
/// control takes no part in it: the field reads the property the expression names.
/// </summary>
public sealed class FieldValidationTests : BunitContext
{
    public FieldValidationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private static string? Error(IRenderedComponent<FieldForm> cut, string field) =>
        cut.FindAll($"[data-testid={field}] [data-slot=field-error]").FirstOrDefault()?.TextContent.Trim();

    private static bool Invalid(IRenderedComponent<FieldForm> cut, string field) =>
        cut.Find($"[data-testid={field}]").GetAttribute("data-invalid") == "true";

    [Fact]
    public void Nothing_is_announced_or_marked_before_the_first_edit()
    {
        var cut = Render<FieldForm>();

        Assert.Empty(cut.Instance.Notified);
        Assert.False(Invalid(cut, "email"));
        Assert.False(cut.Instance.Context.IsModified());
    }

    [Fact]
    public void Typing_validates_the_field_without_a_submit()
    {
        var cut = Render<FieldForm>();

        cut.Find("[data-testid=email] input").Input("not an address");

        Assert.Equal(["Email"], cut.Instance.Notified);
        Assert.Equal("Email is not valid.", Error(cut, "email"));
        Assert.True(Invalid(cut, "email"));
        Assert.Equal("true", cut.Find("[data-testid=email] input").GetAttribute("aria-invalid"));

        cut.Find("[data-testid=email] input").Input("ada@example.com");

        Assert.Null(Error(cut, "email"));
        Assert.False(Invalid(cut, "email"));
    }

    [Fact]
    public void A_control_that_is_not_a_text_box_is_followed_the_same_way()
    {
        var cut = Render<FieldForm>();

        // On, then off again: off is the invalid state, and only an edit can show it.
        cut.Find("[data-testid=accept] [role=switch]").Click();
        Assert.Null(Error(cut, "accept"));
        cut.Find("[data-testid=accept] [role=switch]").Click();

        Assert.Equal(["Accept", "Accept"], cut.Instance.Notified);
        Assert.Equal("Accept the terms.", Error(cut, "accept"));
    }

    [Fact]
    public void A_property_of_a_nested_object_is_named_by_its_own_object()
    {
        var cut = Render<FieldForm>();

        cut.Find("[data-testid=city] input").Input("");

        Assert.Equal("City is required.", Error(cut, "city"));
        // The field is the nested object's, which a plain name on the form's model cannot say.
        var field = new Microsoft.AspNetCore.Components.Forms.FieldIdentifier(cut.Instance.Model.Address, "City");
        Assert.True(cut.Instance.Context.IsModified(field));
    }

    [Fact]
    public void A_path_through_an_indexer_is_followed()
    {
        var cut = Render<FieldForm>();

        cut.Find("[data-testid=line] input").Input("");

        Assert.Equal("Text is required.", Error(cut, "line"));
    }

    [Fact]
    public void Name_alone_keeps_waiting_for_a_submit()
    {
        var cut = Render<FieldForm>();

        cut.Find("[data-testid=nickname] input").Input("");

        Assert.Empty(cut.Instance.Notified);
        Assert.Null(Error(cut, "nickname"));

        // Its messages still arrive once the form validates.
        cut.InvokeAsync(() => cut.Instance.Context.Validate());
        Assert.Equal("Nickname is required.", Error(cut, "nickname"));
    }

    [Fact]
    public void An_input_that_notifies_by_itself_is_not_announced_twice()
    {
        var cut = Render<FieldForm>();

        cut.Find("[data-testid=phone] input").Change("");

        Assert.Equal(["Phone"], cut.Instance.Notified);
        Assert.Equal("Phone is required.", Error(cut, "phone"));
    }

    [Fact]
    public void A_value_written_from_code_is_announced_too()
    {
        var cut = Render<FieldForm>();

        cut.InvokeAsync(() => cut.Instance.Change(m => m.Email = "nope"));

        Assert.Equal(["Email"], cut.Instance.Notified);
        Assert.Equal("Email is not valid.", Error(cut, "email"));
    }

    [Fact]
    public void Loading_another_model_is_not_an_edit()
    {
        var cut = Render<FieldForm>();

        cut.InvokeAsync(() => cut.Instance.Load(new FieldForm.Signup { Email = "someone@example.com" }));

        Assert.Empty(cut.Instance.Notified);
        Assert.False(cut.Instance.Context.IsModified());
        Assert.False(Invalid(cut, "email"));

        // And the new model's fields are followed from there.
        cut.Find("[data-testid=email] input").Input("broken");
        Assert.Equal(["Email"], cut.Instance.Notified);
    }

    [Fact]
    public void A_path_that_ends_nowhere_shows_no_messages_and_throws_nothing()
    {
        // An object on the way is null: the field has nothing to name until it is back.
        var model = new FieldForm.Signup { Address = null! };
        var context = new EditContext(model);
        Expression<Func<object?>> city = () => model.Address.City;

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddComponentParameter(1, "Value", context);
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<BzField>(0);
                inner.AddComponentParameter(1, nameof(BzField.For), city);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Null(cut.Find("[data-slot=field]").GetAttribute("data-invalid"));
    }

    [Fact]
    public void Outside_a_form_For_is_ignored_and_Error_still_works()
    {
        var model = new FieldForm.Signup();
        Expression<Func<object?>> email = () => model.Email;

        var cut = Render<BzField>(p => p.Add(x => x.For, email).Add(x => x.Error, "Something is wrong."));

        Assert.Equal("true", cut.Find("[data-slot=field]").GetAttribute("data-invalid"));
    }
}
