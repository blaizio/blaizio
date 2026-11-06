using System.ComponentModel.DataAnnotations;

namespace Blaizio.Ui.Tests;

public enum Role { Member, Admin }

/// <summary>A mutable row: every editable property has a setter the table writes through.</summary>
public sealed class Person
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    [Range(0, 150)] public int Age { get; set; }
    public int? Score { get; set; }
    public bool Active { get; set; }
    public Role Role { get; set; }
    public string Code { get; set; } = "";
    public DateOnly Joined { get; set; } = new(2024, 1, 15);

    public static List<Person> Sample() =>
    [
        new() { Id = 1, Name = "Ada", Age = 36, Score = 90, Active = true, Role = Role.Admin, Code = "A1" },
        new() { Id = 2, Name = "Bo", Age = 28, Score = null, Active = false, Role = Role.Member, Code = "B2" },
        new() { Id = 3, Name = "Cy", Age = 51, Score = 70, Active = true, Role = Role.Member, Code = "C3" },
    ];
}
