using System.Linq.Expressions;
using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The query builder, against an in-memory sequence.
///
/// <b>No database, and that is the point.</b> What is being tested here is the
/// composition of expression trees — which operator produces which predicate,
/// which column keys are refused, how sort keys stack. Those are true of the
/// expression regardless of what executes it, and a test that needed PostgreSQL to
/// establish them would be slower and prove no more. What genuinely needs a
/// database — that the predicates translate to SQL at all, and that RLS holds — is
/// checked separately against a real server.
/// </summary>
public class ReportQueryBuilderTests
{
    private sealed record Row(
        long Id, string Account, string? Reference, decimal Debit, DateOnly Date);

    private static readonly IQueryable<Row> Rows = new[]
    {
        new Row(1, "Cash", "INV-1", 100m, new DateOnly(2026, 4, 1)),
        new Row(2, "Bank", null, 250m, new DateOnly(2026, 4, 15)),
        new Row(3, "Cash", "INV-3", 75m, new DateOnly(2026, 5, 2)),
        new Row(4, "Sales", "REF-4", 500m, new DateOnly(2026, 6, 30)),
    }.AsQueryable();

    private static readonly IReadOnlyDictionary<string, ReportColumn> Columns =
        new List<ReportColumn>
        {
            ReportColumn.Of<Row, long>("id", ColumnDataType.Number, r => r.Id),
            ReportColumn.Of<Row, string>("account", ColumnDataType.Text, r => r.Account),
            ReportColumn.Of<Row, string?>("reference", ColumnDataType.Text, r => r.Reference),
            ReportColumn.Of<Row, decimal>(
                "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),
            ReportColumn.Of<Row, DateOnly>("date", ColumnDataType.Date, r => r.Date),
            ReportColumn.Of<Row, string>(
                "locked", ColumnDataType.Text, r => r.Account, sortable: false, filterable: false),
        }.ToDictionary(c => c.Key, StringComparer.Ordinal);

    private static readonly LambdaExpression ById = (Expression<Func<Row, long>>)(r => r.Id);

    private static IQueryable<Row> Filter(params ReportFilterModel[] filters) =>
        ReportQueryBuilder<Row>.ApplyFilters(Rows, filters, Columns);

    private static ReportFilterModel Where(
        string column, FilterOperator op, string? value = null, string? value2 = null) =>
        new() { Column = column, Operator = op, Value = value, Value2 = value2 };

    // ---- the security property ----

    [Fact]
    public void A_filter_on_a_column_the_report_does_not_declare_is_refused()
    {
        ReportQueryException error = Assert.Throws<ReportQueryException>(
            () => Filter(Where("; DROP TABLE Reports", FilterOperator.Equals, "x")).ToList());

        Assert.Contains("has no column", error.Message);
    }

    [Fact]
    public void A_sort_on_a_column_the_report_does_not_declare_is_refused() =>
        Assert.Throws<ReportQueryException>(() => ReportQueryBuilder<Row>.ApplySorts(
            Rows,
            [new ReportSortModel { Column = "nonsense" }],
            Columns,
            ById).ToList());

    [Fact]
    public void An_unknown_column_is_an_error_rather_than_a_dropped_clause()
    {
        // The distinction that matters: a silently ignored filter returns more rows
        // than were asked for, and looks exactly like a correct answer.
        Assert.Throws<ReportQueryException>(() => Filter(Where("ghost", FilterOperator.Equals, "1")).ToList());
        Assert.Equal(4, Rows.Count());
    }

    [Fact]
    public void A_column_marked_unfilterable_refuses_a_filter() =>
        Assert.Throws<ReportQueryException>(
            () => Filter(Where("locked", FilterOperator.Equals, "Cash")).ToList());

    [Fact]
    public void A_column_marked_unsortable_refuses_a_sort() =>
        Assert.Throws<ReportQueryException>(() => ReportQueryBuilder<Row>.ApplySorts(
            Rows, [new ReportSortModel { Column = "locked" }], Columns, ById).ToList());

    // ---- operators ----

    [Fact]
    public void Equals_matches_on_the_columns_own_type()
    {
        List<Row> matched = Filter(Where("account", FilterOperator.Equals, "Cash")).ToList();

        Assert.Equal([1L, 3L], matched.Select(r => r.Id));
    }

    [Fact]
    public void Comparisons_parse_the_value_against_the_column_type()
    {
        // "200" is text on the wire and a decimal in the predicate. Comparing it as
        // text would put 75 above 500.
        List<Row> matched = Filter(Where("debit", FilterOperator.GreaterThan, "200")).ToList();

        Assert.Equal([2L, 4L], matched.Select(r => r.Id));
    }

    [Fact]
    public void Between_is_inclusive_at_both_ends()
    {
        List<Row> matched = Filter(
            Where("date", FilterOperator.Between, "2026-04-01", "2026-05-02")).ToList();

        Assert.Equal([1L, 2L, 3L], matched.Select(r => r.Id));
    }

    [Fact]
    public void Contains_and_starts_with_survive_a_null_without_throwing()
    {
        // Row 2's reference is null. In SQL a null simply never matches; the same
        // expression run in memory would throw without the guard.
        Assert.Equal([1L, 3L], Filter(Where("reference", FilterOperator.Contains, "INV"))
            .ToList().Select(r => r.Id));

        Assert.Equal([4L], Filter(Where("reference", FilterOperator.StartsWith, "REF"))
            .ToList().Select(r => r.Id));
    }

    [Fact]
    public void In_matches_any_of_its_values()
    {
        ReportFilterModel filter = new()
        {
            Column = "account",
            Operator = FilterOperator.In,
            Values = ["Cash", "Sales"],
        };

        Assert.Equal([1L, 3L, 4L], Filter(filter).ToList().Select(r => r.Id));
    }

    [Fact]
    public void Not_in_is_the_complement_of_in()
    {
        ReportFilterModel filter = new()
        {
            Column = "account",
            Operator = FilterOperator.NotIn,
            Values = ["Cash"],
        };

        Assert.Equal([2L, 4L], Filter(filter).ToList().Select(r => r.Id));
    }

    [Fact]
    public void An_empty_in_list_is_refused_rather_than_matching_nothing() =>
        Assert.Throws<ReportQueryException>(() => Filter(new ReportFilterModel
        {
            Column = "account",
            Operator = FilterOperator.In,
        }).ToList());

    [Fact]
    public void Is_null_and_is_not_null_split_the_set()
    {
        Assert.Equal([2L], Filter(Where("reference", FilterOperator.IsNull)).ToList().Select(r => r.Id));
        Assert.Equal(3, Filter(Where("reference", FilterOperator.IsNotNull)).Count());
    }

    [Fact]
    public void A_value_that_will_not_parse_says_so_in_words_a_person_can_act_on()
    {
        ReportQueryException error = Assert.Throws<ReportQueryException>(
            () => Filter(Where("date", FilterOperator.Equals, "not-a-date")).ToList());

        Assert.Contains("not a valid value", error.Message);
        Assert.Contains("date", error.Message);
    }

    [Fact]
    public void Filters_combine_with_and()
    {
        List<Row> matched = Filter(
            Where("account", FilterOperator.Equals, "Cash"),
            Where("debit", FilterOperator.GreaterThan, "80")).ToList();

        Assert.Equal([1L], matched.Select(r => r.Id));
    }

    // ---- ordering ----

    [Fact]
    public void Sort_keys_apply_in_the_order_given()
    {
        List<Row> sorted = ReportQueryBuilder<Row>.ApplySorts(
            Rows,
            [
                new ReportSortModel { Column = "account" },
                new ReportSortModel { Column = "debit", Direction = SortDirection.Desc },
            ],
            Columns,
            ById).ToList();

        Assert.Equal(["Bank", "Cash", "Cash", "Sales"], sorted.Select(r => r.Account));
        Assert.Equal([2L, 1L, 3L, 4L], sorted.Select(r => r.Id));
    }

    [Fact]
    public void A_request_with_no_sort_still_gets_a_total_order()
    {
        // Postgres promises no order without ORDER BY, so paging an unordered query
        // can show one row twice and another never.
        List<Row> sorted = ReportQueryBuilder<Row>.ApplySorts(Rows, [], Columns, ById).ToList();

        Assert.Equal([1L, 2L, 3L, 4L], sorted.Select(r => r.Id));
    }

    [Fact]
    public void The_fallback_breaks_ties_the_sort_keys_leave()
    {
        List<Row> sorted = ReportQueryBuilder<Row>.ApplySorts(
            Rows, [new ReportSortModel { Column = "account" }], Columns, ById).ToList();

        // Both Cash rows tie on the only sort key; the tie-break settles them.
        Assert.Equal([2L, 1L, 3L, 4L], sorted.Select(r => r.Id));
    }

    // ---- paging ----

    [Theory]
    [InlineData(1, 2, new[] { 1L, 2L })]
    [InlineData(2, 2, new[] { 3L, 4L })]
    [InlineData(3, 2, new long[0])]
    public void Paging_walks_the_ordered_set(int number, int size, long[] expected)
    {
        IQueryable<Row> ordered = ReportQueryBuilder<Row>.ApplySorts(Rows, [], Columns, ById);

        List<Row> page = ReportQueryBuilder<Row>
            .Page(ordered, new ReportPageModel { Number = number, Size = size })
            .ToList();

        Assert.Equal(expected, page.Select(r => r.Id));
    }

    // ---- reading values back ----

    [Fact]
    public void A_column_reads_its_value_off_a_materialised_row()
    {
        Row row = new(9, "Cash", null, 12.5m, new DateOnly(2026, 4, 1));

        Assert.Equal("Cash", Columns["account"].Read(row));
        Assert.Equal(12.5m, Columns["debit"].Read(row));
        Assert.Null(Columns["reference"].Read(row));
    }

    [Fact]
    public void Only_columns_declaring_an_aggregate_are_totalled()
    {
        Assert.True(Columns["debit"].IsAggregatable);
        Assert.False(Columns["account"].IsAggregatable);
        Assert.False(Columns["date"].IsAggregatable);
    }
}
