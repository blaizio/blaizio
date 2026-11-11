using System.Globalization;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// A number cell edited in place. Its field is a BzInputNumber, which takes "." as the decimal
/// point in every culture (numeric keypads send it) and never holds grouping. The cell parser
/// also accepts grouping, so where "." groups thousands a typed "1.5" used to commit 15.
/// </summary>
public sealed class DataTableNumberCellTests : BunitContext
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public DataTableNumberCellTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        base.Dispose(disposing);
    }

    private static bool Ok(object result) => (bool)result.GetType().GetProperty("ok")!.GetValue(result)!;

    private async Task<(PriceGrid.Product Row, object Result)> Commit(string culture, DataTableEditMode mode, string column, int cell, string text)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        var row = new PriceGrid.Product { Id = 1, Price = 10m, Stock = 4 };
        var cut = Render<PriceGrid>(p => p.Add(x => x.Items, [row]).Add(x => x.Mode, mode));
        var table = cut.Instance.Table!;

        await cut.InvokeAsync(() => table.BeginEdit(0, cell, null, "key", false));
        var result = await cut.InvokeAsync(() => table.EndEdit(true, new Dictionary<string, string?> { [column] = text }, false));
        return (row, result);
    }

    [Theory]
    [InlineData("de-DE", "1.5", 1.5)]   // the keypad's dot, where "." groups thousands
    [InlineData("de-DE", "1,5", 1.5)]   // the culture's own separator
    [InlineData("es-ES", "0.25", 0.25)]
    [InlineData("en-US", "1.5", 1.5)]
    public async Task A_typed_dot_is_the_decimal_point_in_every_culture(string culture, string text, double expected)
    {
        var (row, result) = await Commit(culture, DataTableEditMode.Cell, "Price", 0, text);

        Assert.True(Ok(result));
        Assert.Equal((decimal)expected, row.Price);
    }

    [Fact]
    public async Task Row_mode_reads_the_typed_dot_the_same_way()
    {
        var (row, result) = await Commit("de-DE", DataTableEditMode.Row, "Price", 0, "1.5");

        Assert.True(Ok(result));
        Assert.Equal(1.5m, row.Price);
    }

    [Fact]
    public async Task A_whole_number_cell_refuses_a_fraction_instead_of_reading_it_as_grouping()
    {
        // Read with the culture as-is, "1.5" in a German int cell was 15.
        var (row, result) = await Commit("de-DE", DataTableEditMode.Cell, "Stock", 1, "1.5");

        Assert.False(Ok(result));
        Assert.Equal(4, row.Stock);
    }
}
