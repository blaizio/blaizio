namespace Blaizio.Ui.Tests;

public enum Level { Low, High }

/// <summary>A row for the localization tests: a nullable enum (a pick list with a blank option) and a column with Options.</summary>
public sealed class Ticket
{
    public int Id { get; init; }
    public string Title { get; set; } = "";
    public int Hours { get; set; }
    public DateOnly Due { get; set; } = new(2026, 11, 8);
    public Level? Priority { get; set; }
    public string Status { get; set; } = "Open";

    public static List<Ticket> Sample() =>
    [
        new() { Id = 1, Title = "Login", Hours = 3, Priority = Level.High },
        new() { Id = 2, Title = "Export", Hours = 5 },
        new() { Id = 3, Title = "Search", Hours = 2, Priority = Level.Low, Status = "Closed" },
        new() { Id = 4, Title = "Avatars", Hours = 8 },
        new() { Id = 5, Title = "Billing", Hours = 13, Priority = Level.High },
    ];
}
