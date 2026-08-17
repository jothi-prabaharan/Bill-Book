using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The composite group key.
///
/// Grouping runs in SQL on a single concatenated string rather than on a key
/// object, so what needs establishing here is that the concatenation round-trips:
/// three levels in, three levels out, with nulls and empty values surviving as
/// their own group rather than merging into a neighbour's.
/// </summary>
public class GroupKeyTests
{
    private sealed record Row(string Type, string? Account, decimal Debit);

    private static readonly IQueryable<Row> Rows = new[]
    {
        new Row("Asset", "Cash", 100m),
        new Row("Asset", "Cash", 50m),
        new Row("Asset", "Bank", 250m),
        new Row("Income", null, 400m),
        new Row("Income", null, 25m),
    }.AsQueryable();

    private static readonly IReadOnlyDictionary<string, ReportColumn> Columns =
        new List<ReportColumn>
        {
            ReportColumn.Of<Row, string>(
                "type", ColumnDataType.Text, r => r.Type, groupable: true),
            ReportColumn.Of<Row, string?>(
                "account", ColumnDataType.Text, r => r.Account, groupable: true),
            ReportColumn.Of<Row, decimal>(
                "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),
        }.ToDictionary(c => c.Key, StringComparer.Ordinal);

    [Fact]
    public void A_groupable_column_must_be_text()
    {
        // Grouping concatenates the key in SQL. Concatenating a date asks Postgres
        // to render it in a format nobody chose and which moves with server
        // settings — so the constraint is enforced where the column is declared,
        // not discovered when a report is run.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ReportColumn.Of<Row, decimal>(
                "debit", ColumnDataType.Money, r => r.Debit, groupable: true));

        Assert.Contains("must be text", error.Message);
    }

    [Fact]
    public void One_level_groups_on_that_column()
    {
        var groups = Rows
            .GroupBy(ReportQueryBuilder<Row>.GroupKeySelector(["type"], Columns).Compile())
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Debit));

        Assert.Equal(400m, groups["Asset"]);
        Assert.Equal(425m, groups["Income"]);
    }

    [Fact]
    public void Levels_round_trip_through_the_separator()
    {
        var groups = Rows
            .GroupBy(ReportQueryBuilder<Row>
                .GroupKeySelector(["type", "account"], Columns).Compile())
            .Select(g => new
            {
                Path = g.Key.Split(ReportQueryBuilder<Row>.GroupSeparator),
                Total = g.Sum(r => r.Debit),
            })
            .ToList();

        Assert.Equal(3, groups.Count);
        Assert.All(groups, g => Assert.Equal(2, g.Path.Length));

        var cash = groups.Single(g => g.Path is ["Asset", "Cash"]);
        Assert.Equal(150m, cash.Total);
    }

    [Fact]
    public void A_null_group_value_is_its_own_group_rather_than_a_missing_row()
    {
        // The two Income rows have no account. They must still total together and
        // still appear — a group that vanishes takes its rows' money with it.
        var groups = Rows
            .GroupBy(ReportQueryBuilder<Row>
                .GroupKeySelector(["type", "account"], Columns).Compile())
            .Select(g => new
            {
                Path = g.Key.Split(ReportQueryBuilder<Row>.GroupSeparator),
                Total = g.Sum(r => r.Debit),
            })
            .ToList();

        var income = groups.Single(g => g.Path[0] == "Income");

        Assert.Equal(string.Empty, income.Path[1]);
        Assert.Equal(425m, income.Total);
        Assert.Equal(825m, groups.Sum(g => g.Total));
    }

    [Fact]
    public void Grouping_by_a_column_the_report_does_not_declare_is_refused() =>
        Assert.Throws<ReportQueryException>(
            () => ReportQueryBuilder<Row>.GroupKeySelector(["ghost"], Columns));

    [Fact]
    public void Grouping_by_a_column_not_marked_groupable_is_refused() =>
        Assert.Throws<ReportQueryException>(
            () => ReportQueryBuilder<Row>.GroupKeySelector(["debit"], Columns));

    [Fact]
    public void Grouping_by_nothing_is_refused_rather_than_grouping_everything() =>
        Assert.Throws<ReportQueryException>(
            () => ReportQueryBuilder<Row>.GroupKeySelector([], Columns));
}
