using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The pivot builder, against an in-memory sequence.
///
/// The shape is what is being established: that a matrix comes back with the
/// columns the data produced rather than the ones asked for, that every declared
/// column is present on every row even when empty, and that a column axis with
/// too many values is refused rather than run.
/// </summary>
public class PivotTests
{
    private sealed record Row(string Type, string Month, decimal Amount);

    private static readonly List<Row> Rows = new[]
    {
        new Row("Asset", "Apr", 100m),
        new Row("Asset", "Apr", 50m),
        new Row("Asset", "May", 75m),
        new Row("Income", "Apr", 400m),
        // Nothing for Income in May — the cell must still exist, empty.
    }.ToList();

    private static readonly IQueryable<Row> Query = AsyncQueryable.Of(Rows);

    private static readonly IReadOnlyDictionary<string, ReportColumn> Columns =
        new List<ReportColumn>
        {
            ReportColumn.Of<Row, string>("type", ColumnDataType.Text, r => r.Type, groupable: true),
            ReportColumn.Of<Row, string>("month", ColumnDataType.Text, r => r.Month, groupable: true),
            ReportColumn.Of<Row, decimal>(
                "amount", ColumnDataType.Money, r => r.Amount, AggregateFunction.Sum),
        }.ToDictionary(c => c.Key, StringComparer.Ordinal);

    private static ReportPivotModel Pivot(params string[] columnAxis) => new()
    {
        Rows = ["type"],
        Columns = [.. columnAxis],
        Values = [new ReportPivotValueModel { Column = "amount", Aggregate = AggregateFunction.Sum }],
    };

    private static Task<ReportResultView> Build(ReportPivotModel pivot) =>
        PivotBuilder<Row>.BuildAsync(Query, pivot, Columns, CancellationToken.None);

    [Fact]
    public async Task The_column_axis_comes_from_the_data()
    {
        // Two months in the data means two value columns, plus the row label. The
        // caller could not have known that in advance, which is why the response
        // declares its columns.
        ReportResultView matrix = await Build(Pivot("month"));

        Assert.Equal(3, matrix.Columns.Count);
        Assert.Equal("pivotRow", matrix.Columns[0].Key);
        Assert.Contains(matrix.Columns, c => c.Header == "Apr");
        Assert.Contains(matrix.Columns, c => c.Header == "May");
    }

    [Fact]
    public async Task Cells_hold_the_aggregate_for_their_row_and_column()
    {
        ReportResultView matrix = await Build(Pivot("month"));

        Dictionary<string, object?> asset = matrix.Rows.Single(r => (string?)r["pivotRow"] == "Asset");

        Assert.Equal(150m, asset["Apr|amount"]);
        Assert.Equal(75m, asset["May|amount"]);
    }

    [Fact]
    public async Task A_cell_with_no_data_is_present_and_empty()
    {
        // Absent rather than zero, and present rather than missing: a missing key
        // leaves the grid rendering a ragged row and misaligning every column
        // after it.
        ReportResultView matrix = await Build(Pivot("month"));

        Dictionary<string, object?> income =
            matrix.Rows.Single(r => (string?)r["pivotRow"] == "Income");

        Assert.True(income.ContainsKey("May|amount"));
        Assert.Null(income["May|amount"]);
    }

    [Fact]
    public async Task A_pivot_with_no_column_axis_is_a_summary_table()
    {
        // Legitimate, and what somebody asked for when they picked no column field.
        ReportResultView matrix = await Build(Pivot());

        Assert.Equal(2, matrix.Columns.Count);
        Assert.Equal(2, matrix.Rows.Count);
    }

    [Fact]
    public async Task A_pivot_needs_a_row_field_and_a_value_field()
    {
        await Assert.ThrowsAsync<ReportQueryException>(() => Build(new ReportPivotModel
        {
            Values = [new ReportPivotValueModel { Column = "amount" }],
        }));

        await Assert.ThrowsAsync<ReportQueryException>(() => Build(new ReportPivotModel
        {
            Rows = ["type"],
        }));
    }

    [Fact]
    public async Task A_column_axis_the_report_does_not_declare_is_refused() =>
        await Assert.ThrowsAsync<ReportQueryException>(() => Build(Pivot("ghost")));

    [Fact]
    public async Task Too_many_columns_is_refused_by_name_rather_than_run()
    {
        // A pivot by contact across thousands of contacts is not a report, it is a
        // spreadsheet nobody can read. The message names the field so the person
        // can pick another rather than watching it hang.
        IQueryable<Row> wide = AsyncQueryable.Of(Enumerable
            .Range(0, PivotBuilder<Row>.MaxColumns + 1)
            .Select(index => new Row("Asset", $"M{index:D4}", 1m))
            .ToList());

        ReportQueryException error = await Assert.ThrowsAsync<ReportQueryException>(
            () => PivotBuilder<Row>.BuildAsync(
                wide, Pivot("month"), Columns, CancellationToken.None));

        Assert.Contains("month", error.Message);
        Assert.Contains("limit is 200", error.Message);
    }
}
