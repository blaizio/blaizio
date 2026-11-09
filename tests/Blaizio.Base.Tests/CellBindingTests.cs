using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Blaizio;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>Pure tests for the read / parse / validate / write half of an editable table cell.</summary>
public class CellBindingTests
{
    private enum Status { Active, Paused }

    [Flags]
    private enum Perms { None = 0, Read = 1, Write = 2 }

    private sealed class Row
    {
        [Required] public string Name { get; set; } = "";
        [Range(0, 150)] public int Age { get; set; }
        [Display(Name = "Unit price")] public decimal Price { get; set; }
        public double Ratio { get; set; }
        public int? Score { get; set; }
        public bool Active { get; set; }
        public bool? Flag { get; set; }
        public Status State { get; set; }
        public Status? MaybeState { get; set; }
        public Perms Access { get; set; }
        public DateOnly Day { get; set; }
        public DateTime When { get; set; }
        public TimeOnly At { get; set; }
        public Guid Id { get; set; }
        public char Grade { get; set; }
        [Silent] public int Quiet { get; set; }
        public string Code { get; init; } = "";
        public int Computed => Age * 2;
        public Address Home { get; set; } = new();
        public Point Spot { get; set; }
        public string? Field;
    }

    private sealed class Address
    {
        public string City { get; set; } = "";
    }

    private struct Point
    {
        public int X { get; set; }
    }

    private sealed record Item(string Name);

    // A rule that refuses every value without a message of its own.
    private sealed class SilentAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) => new(null);
    }

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Reads_and_writes_a_settable_property()
    {
        var binding = new CellBinding<Row, int>(r => r.Age);
        var row = new Row { Age = 3 };

        Assert.True(binding.CanWrite);
        Assert.Equal(3, binding.Read(row));
        binding.Write(row, 41);
        Assert.Equal(41, row.Age);
        Assert.Equal("Age", binding.MemberName);
    }

    [Fact]
    public void Writes_through_a_nested_reference_and_a_field()
    {
        var row = new Row();
        new CellBinding<Row, string>(r => r.Home.City).Write(row, "Dubai");
        new CellBinding<Row, string?>(r => r.Field).Write(row, "x");

        Assert.Equal("Dubai", row.Home.City);
        Assert.Equal("x", row.Field);
    }

    [Fact]
    public void Init_computed_record_and_struct_members_are_not_writable()
    {
        Assert.False(new CellBinding<Row, string>(r => r.Code).CanWrite);
        Assert.False(new CellBinding<Row, int>(r => r.Computed).CanWrite);
        Assert.False(new CellBinding<Item, string>(i => i.Name).CanWrite);
        // Assigning through the struct's copy would silently lose the edit.
        Assert.False(new CellBinding<Row, int>(r => r.Spot.X).CanWrite);

        var binding = new CellBinding<Row, string>(r => r.Code);
        Assert.Throws<InvalidOperationException>(() => binding.Write(new Row(), "x"));
    }

    [Fact]
    public void Kind_follows_the_value_type()
    {
        Assert.Equal(CellEditorKind.Text, new CellBinding<Row, string>(r => r.Name).Kind);
        Assert.Equal(CellEditorKind.Text, new CellBinding<Row, decimal>(r => r.Price).Kind);
        Assert.Equal(CellEditorKind.Text, new CellBinding<Row, DateOnly>(r => r.Day).Kind);
        Assert.Equal(CellEditorKind.Text, new CellBinding<Row, Guid>(r => r.Id).Kind);
        Assert.Equal(CellEditorKind.Toggle, new CellBinding<Row, bool>(r => r.Active).Kind);
        Assert.Equal(CellEditorKind.Toggle, new CellBinding<Row, bool?>(r => r.Flag).Kind);
        Assert.Equal(CellEditorKind.Choice, new CellBinding<Row, Status>(r => r.State).Kind);
        Assert.Equal(CellEditorKind.Text, new CellBinding<Row, Perms>(r => r.Access).Kind); // flags combine: typed
        Assert.Equal(CellEditorKind.None, new CellBinding<Row, Address>(r => r.Home).Kind);
    }

    [Fact]
    public void Choices_list_the_enum_names_and_a_blank_for_nullable()
    {
        var plain = new CellBinding<Row, Status>(r => r.State).Choices;
        var nullable = new CellBinding<Row, Status?>(r => r.MaybeState).Choices;

        Assert.Equal(["Active", "Paused"], plain.Select(c => c.Value));
        Assert.Equal(["", "Active", "Paused"], nullable.Select(c => c.Value));
    }

    [Fact]
    public void Formats_the_raw_value_for_editing()
    {
        Assert.Equal("1234.5", new CellBinding<Row, decimal>(r => r.Price).Format(1234.5m, Us));
        Assert.Equal("1234,5", new CellBinding<Row, decimal>(r => r.Price).Format(1234.5m, German));
        Assert.Equal("0.1", new CellBinding<Row, double>(r => r.Ratio).Format(0.1, Us));
        Assert.Equal("11/4/2026", new CellBinding<Row, DateOnly>(r => r.Day).Format(new DateOnly(2026, 11, 4), Us));
        Assert.Equal("11/4/2026", new CellBinding<Row, DateTime>(r => r.When).Format(new DateTime(2026, 11, 4), Us));
        Assert.Equal("11/4/2026 3:05:00 PM", new CellBinding<Row, DateTime>(r => r.When).Format(new DateTime(2026, 11, 4, 15, 5, 0), Us));
        Assert.Equal("", new CellBinding<Row, int?>(r => r.Score).Format(null, Us));
        Assert.Equal("Paused", new CellBinding<Row, Status>(r => r.State).Format(Status.Paused, Us));
    }

    [Fact]
    public void Parses_in_the_given_culture()
    {
        var price = new CellBinding<Row, decimal>(r => r.Price);

        Assert.True(price.TryParse("1,234.50", out var us, out _, culture: Us));
        Assert.Equal(1234.50m, us);
        Assert.True(price.TryParse("1.234,50", out var de, out _, culture: German));
        Assert.Equal(1234.50m, de);

        Assert.True(new CellBinding<Row, DateOnly>(r => r.Day).TryParse("12/24/2026", out var day, out _, culture: Us));
        Assert.Equal(new DateOnly(2026, 12, 24), day);
        Assert.True(new CellBinding<Row, TimeOnly>(r => r.At).TryParse("9:30 PM", out var at, out _, culture: Us));
        Assert.Equal(new TimeOnly(21, 30), at);
        Assert.True(new CellBinding<Row, Status>(r => r.State).TryParse("paused", out var state, out _, culture: Us));
        Assert.Equal(Status.Paused, state);
        Assert.True(new CellBinding<Row, Perms>(r => r.Access).TryParse("Read, Write", out var perms, out _, culture: Us));
        Assert.Equal(Perms.Read | Perms.Write, perms);
        Assert.True(new CellBinding<Row, bool>(r => r.Active).TryParse("TRUE", out var active, out _, culture: Us));
        Assert.True(active);
    }

    [Fact]
    public void Text_is_taken_as_typed_and_blank_is_null_only_when_nullable()
    {
        Assert.True(new CellBinding<Row, string>(r => r.Name).TryParse("  spaced ", out var text, out _));
        Assert.Equal("  spaced ", text);

        Assert.True(new CellBinding<Row, int?>(r => r.Score).TryParse("  ", out var score, out _));
        Assert.Null(score);

        Assert.False(new CellBinding<Row, int>(r => r.Age).TryParse("", out _, out var error, "Age"));
        Assert.Equal("The Age field is required.", error);
    }

    [Fact]
    public void Parse_errors_name_the_field_by_kind()
    {
        Assert.False(new CellBinding<Row, int>(r => r.Age).TryParse("abc", out _, out var number, "Age", Us));
        Assert.False(new CellBinding<Row, DateOnly>(r => r.Day).TryParse("soon", out _, out var date, "Day", Us));
        Assert.False(new CellBinding<Row, TimeOnly>(r => r.At).TryParse("noonish", out _, out var time, "At", Us));
        Assert.False(new CellBinding<Row, Status>(r => r.State).TryParse("Gone", out _, out var choice, "State", Us));
        // A bare number that names no member is not a choice.
        Assert.False(new CellBinding<Row, Status>(r => r.State).TryParse("42", out _, out _, "State", Us));

        Assert.Equal("The Age field must be a number.", number);
        Assert.Equal("The Day field must be a date.", date);
        Assert.Equal("The At field must be a time.", time);
        Assert.Equal("The State field must be one of: Active, Paused.", choice);
    }

    [Fact]
    public void A_message_builder_words_every_refusal_from_its_kind()
    {
        var seen = new List<CellParseError>();
        string Hebrew(CellParseError e)
        {
            seen.Add(e);
            return $"he:{e.Kind}:{e.Field}";
        }

        Assert.False(new CellBinding<Row, int>(r => r.Age).TryParse("", out _, out var required, "Age", Us, Hebrew));
        Assert.False(new CellBinding<Row, int>(r => r.Age).TryParse("abc", out _, out var number, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, DateOnly>(r => r.Day).TryParse("soon", out _, out var date, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, TimeOnly>(r => r.At).TryParse("noonish", out _, out var time, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, bool>(r => r.Active).TryParse("maybe", out _, out var boolean, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, char>(r => r.Grade).TryParse("AB", out _, out var character, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, Status>(r => r.State).TryParse("Gone", out _, out var choice, null, Us, Hebrew));
        Assert.False(new CellBinding<Row, Guid>(r => r.Id).TryParse("not-a-guid", out _, out var invalid, null, Us, Hebrew));

        Assert.Equal("he:Required:Age", required);
        Assert.Equal("he:Number:Age", number);
        Assert.Equal("he:Date:Day", date);
        Assert.Equal("he:Time:At", time);
        Assert.Equal("he:Boolean:Active", boolean);
        Assert.Equal("he:Character:Grade", character);
        Assert.Equal("he:Choice:State", choice);
        Assert.Equal("he:Invalid:Id", invalid);
        // A choice carries the names it takes; the others carry none.
        Assert.Equal(["Active", "Paused"], seen.Single(e => e.Kind == CellParseErrorKind.Choice).Options);
        Assert.All(seen.Where(e => e.Kind != CellParseErrorKind.Choice), e => Assert.Empty(e.Options));
    }

    [Fact]
    public void The_english_message_stays_when_the_builder_gives_back_nothing()
    {
        Assert.False(new CellBinding<Row, int>(r => r.Age).TryParse("abc", out _, out var error, "Age", Us, _ => ""));
        Assert.Equal("The Age field must be a number.", error);

        Assert.Equal("The Role field must be one of: A, B.", new CellParseError(CellParseErrorKind.Choice, "Role", ["A", "B"]).Message);
        Assert.Equal("The Grade field must be a single character.", new CellParseError(CellParseErrorKind.Character, "Grade", []).Message);
        Assert.Equal("The Id field has an invalid value.", new CellParseError(CellParseErrorKind.Invalid, "Id", []).Message);
    }

    [Fact]
    public void A_rule_without_a_message_of_its_own_is_worded_by_the_rule()
    {
        // The attribute's own wording (localized through the attribute), never one of the binding's.
        var quiet = new CellBinding<Row, int>(r => r.Quiet);
        Assert.Equal(new SilentAttribute().FormatErrorMessage("Quiet"), quiet.Validate(new Row(), 1));
    }

    [Fact]
    public void Display_name_falls_back_from_argument_to_attribute_to_member()
    {
        var price = new CellBinding<Row, decimal>(r => r.Price);
        Assert.Equal("Unit price", price.DisplayName);

        Assert.False(price.TryParse("x", out _, out var byAttribute, culture: Us));
        Assert.False(price.TryParse("x", out _, out var byArgument, "Cost", Us));
        Assert.Equal("The Unit price field must be a number.", byAttribute);
        Assert.Equal("The Cost field must be a number.", byArgument);
    }

    [Fact]
    public void Validate_runs_the_member_data_annotations_without_writing()
    {
        var row = new Row { Age = 5 };
        var age = new CellBinding<Row, int>(r => r.Age);
        var name = new CellBinding<Row, string>(r => r.Name);

        Assert.Null(age.Validate(row, 40));
        Assert.Equal("The field Age must be between 0 and 150.", age.Validate(row, 200));
        Assert.Equal("The Name field is required.", name.Validate(row, ""));
        Assert.Equal(5, row.Age);
        Assert.Null(new CellBinding<Row, double>(r => r.Ratio).Validate(row, -1)); // no rules
    }

    [Fact]
    public void Clear_value_exists_for_text_and_nullable_only()
    {
        Assert.True(new CellBinding<Row, string>(r => r.Name).TryGetClearValue(out var text));
        Assert.Equal("", text);
        Assert.True(new CellBinding<Row, int?>(r => r.Score).TryGetClearValue(out var score));
        Assert.Null(score);
        Assert.False(new CellBinding<Row, int>(r => r.Age).TryGetClearValue(out _));
        Assert.False(new CellBinding<Row, bool>(r => r.Active).TryGetClearValue(out _));
    }

    [Fact]
    public void Null_and_empty_text_count_as_the_same_value()
    {
        Assert.True(CellBinding<Row, string?>.ValuesEqual(null, ""));
        Assert.False(CellBinding<Row, string?>.ValuesEqual("a", ""));
        Assert.True(CellBinding<Row, int?>.ValuesEqual(null, null));
        Assert.False(CellBinding<Row, int?>.ValuesEqual(null, 0));
        Assert.True(CellBinding<Row, decimal>.ValuesEqual(1.50m, 1.5m));
    }

    [Fact]
    public void Unsigned_whole_numbers_hint_a_numeric_keypad()
    {
        Assert.Equal("numeric", new CellBinding<Unsigned, uint>(u => u.Count).InputMode);
        Assert.Null(new CellBinding<Row, int>(r => r.Age).InputMode); // a minus sign has to stay typeable
    }

    private sealed class Unsigned
    {
        public uint Count { get; set; }
    }
}
