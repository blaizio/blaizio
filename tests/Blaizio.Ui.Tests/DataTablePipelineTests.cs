using System.Collections;
using Blaizio;
using Blaizio.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blaizio.Ui.Tests;

/// <summary>
/// Child columns register one by one while the table first renders, and unregister one by one
/// while it is taken down. Each of those used to filter, sort and materialise every row; now the
/// rows are worked out once, by the render the registrations ask for.
/// </summary>
public sealed class DataTablePipelineTests : BunitContext
{
    public DataTablePipelineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlaizio();
    }

    private sealed class CountedRows(List<Person> rows) : IEnumerable<Person>
    {
        public int Reads { get; private set; }

        public IEnumerator<Person> GetEnumerator()
        {
            Reads++;
            return rows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void The_rows_are_not_worked_out_once_per_column()
    {
        const int Columns = 6;
        var rows = new CountedRows(Person.Sample());

        var cut = Render<PipelineGrid>(p => p.Add(x => x.Items, rows));

        // Sorted by the first column, descending: the pipeline did run with the columns in place.
        Assert.Equal(["Cy", "Bo", "Ada"], cut.FindAll("tbody tr").Select(r => r.QuerySelector("td")!.TextContent.Trim()));
        Assert.True(rows.Reads < Columns, $"the rows were read {rows.Reads} times for {Columns} columns");

        var before = rows.Reads;
        cut.Dispose();
        Assert.Equal(before, rows.Reads); // taking the table down reads nothing
    }
}
