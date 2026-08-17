using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Api.Services;

/// <summary>
/// One report. What it is called, what it needs, what it can show, and how to
/// build the query — and <b>nothing about how that query is filtered, sorted,
/// grouped, paged or exported</b>, because the engine does all of that to every
/// report alike.
///
/// That division is the point of the whole design: adding the fifteenth report is
/// writing a column list and a LINQ projection, not writing a report.
/// </summary>
public interface IReportSource
{
    /// <summary>Stable identifier — <c>account-movement</c>. Matches the seeded catalog row.</summary>
    string ReportKey { get; }

    string Title { get; }

    ReportModule Module { get; }

    /// <summary>
    /// The module permission a caller needs beyond <c>reporting.view</c>. A report
    /// the caller lacks it for is absent from the catalog rather than refused, so
    /// the existence of a report is not itself a disclosure.
    /// </summary>
    string RequiredPermission { get; }

    IReadOnlyList<ReportColumn> Columns { get; }

    IReadOnlyList<ReportParameter> Parameters { get; }

    /// <summary>
    /// True when the report's own ordering carries meaning — a running balance, or
    /// anything else defined over an ordered set. The grid then refuses to sort by
    /// anything else, because the figures would be arithmetically true of an order
    /// the reader cannot see.
    /// </summary>
    bool ForcesSortOrder { get; }

    /// <summary>Runs the report. Implemented once, generically, by <see cref="ReportSource{TRow}"/>.</summary>
    Task<ReportResultView> ExecuteAsync(
        ReportQueryRequest request,
        ReportingDbContext db,
        ReportCurrencyView currency,
        CancellationToken ct);
}

/// <summary>
/// The base every report derives from. Holds the one implementation of "run a
/// report" so that no report contains paging, counting or totalling of its own —
/// forty-five copies of that logic being forty-five chances to compute a total
/// two different ways.
/// </summary>
public abstract class ReportSource<TRow> : IReportSource
    where TRow : class
{
    private IReadOnlyDictionary<string, ReportColumn>? _map;

    public abstract string ReportKey { get; }

    public abstract string Title { get; }

    public abstract ReportModule Module { get; }

    public abstract string RequiredPermission { get; }

    public abstract IReadOnlyList<ReportColumn> Columns { get; }

    public virtual IReadOnlyList<ReportParameter> Parameters => [];

    public virtual bool ForcesSortOrder => false;

    /// <summary>
    /// The report's query, executing nothing. Filters, sorting and paging are
    /// composed onto it afterwards, so this must stay an <see cref="IQueryable{T}"/>
    /// — materialising here would page in memory and read the whole ledger to show
    /// fifty rows.
    /// </summary>
    protected abstract IQueryable<TRow> Build(ReportParameters parameters, ReportingDbContext db);

    /// <summary>
    /// The tie-break ordering, applied after the caller's sort keys and used alone
    /// when they name none. Must be unique per row: Postgres promises no order
    /// without one, and paging an unordered query can show a row twice and another
    /// never.
    /// </summary>
    protected abstract LambdaExpression DefaultOrder { get; }

    /// <summary>Column key to column, built once.</summary>
    public IReadOnlyDictionary<string, ReportColumn> Map =>
        _map ??= Columns.ToDictionary(c => c.Key, StringComparer.Ordinal);

    public async Task<ReportResultView> ExecuteAsync(
        ReportQueryRequest request,
        ReportingDbContext db,
        ReportCurrencyView currency,
        CancellationToken ct)
    {
        ReportParameters parameters = new(request.Parameters);
        IQueryable<TRow> query = Build(parameters, db);

        query = ReportQueryBuilder<TRow>.ApplyFilters(query, request.Filters, Map);

        // Held before paging: the footers and totals are of the whole filtered
        // result, which is what makes a subtotal right when its group runs across
        // a page boundary.
        IQueryable<TRow> filtered = query;

        // Counted before paging and before ordering: the count is of the filtered
        // set, and ordering it would cost a sort nobody reads.
        long? total = request.Page.IncludeCount
            ? await query.LongCountAsync(ct)
            : null;

        Dictionary<string, decimal?> totals = await TotalsAsync(query, ct);

        if (ForcesSortOrder && request.Sorts.Count > 0)
        {
            throw new ReportQueryException(
                $"{Title} defines its own order and cannot be sorted by other columns.");
        }

        query = ReportQueryBuilder<TRow>.ApplySorts(query, request.Sorts, Map, DefaultOrder);

        List<TRow> rows = await ReportQueryBuilder<TRow>.Page(query, request.Page)
            .ToListAsync(ct);

        List<ReportColumn> selected = Select(request.Columns);

        return new ReportResultView
        {
            ReportKey = ReportKey,
            Title = Title,
            GeneratedAt = DateTimeOffset.UtcNow,
            Currency = currency,
            Columns = [], // filled by the catalog, which owns presentation
            Rows = [.. rows.Select(row => Project(row, selected))],
            GroupFooters = request.GroupBy.Count == 0
                ? []
                : await GroupFootersAsync(filtered, request.GroupBy, ct),
            GrandTotal = new ReportGroupFooterView
            {
                RowCount = total ?? rows.Count,
                Aggregates = totals,
            },
            Page = new ReportPageView
            {
                Number = request.Page.Number,
                Size = request.Page.Size,
                TotalRows = total,
                TotalPages = total is long count
                    ? (int)Math.Ceiling(count / (double)request.Page.Size)
                    : null,
            },
        };
    }

    /// <summary>
    /// Grand totals, one query per aggregatable column.
    ///
    /// Several small queries rather than one wide projection because the set of
    /// columns varies per request, and building one dynamic projection over a
    /// varying column list is where this would stop being readable. Only columns
    /// that declare an aggregate are totalled at all.
    /// </summary>
    private async Task<Dictionary<string, decimal?>> TotalsAsync(
        IQueryable<TRow> query, CancellationToken ct)
    {
        Dictionary<string, decimal?> totals = [];

        foreach (ReportColumn column in Columns.Where(c => c.IsAggregatable))
        {
            IQueryable<decimal?> values = ReportQueryBuilder<TRow>.SelectDecimal(query, column);

            totals[column.Key] = column.DefaultAggregate switch
            {
                AggregateFunction.Sum => await values.SumAsync(ct),
                AggregateFunction.Min => await values.MinAsync(ct),
                AggregateFunction.Max => await values.MaxAsync(ct),
                AggregateFunction.Avg => await values.AverageAsync(ct),
                AggregateFunction.Count => await values.CountAsync(ct),
                _ => null,
            };
        }

        return totals;
    }

    /// <summary>
    /// Subtotals per group, over the <b>whole filtered result</b> rather than the
    /// page. That distinction is the entire reason this runs as its own query: a
    /// footer computed from the fifty rows on screen is wrong for every group that
    /// spans a page boundary, and wrong in a way that looks plausible.
    ///
    /// One query for the row counts, then one per aggregatable column. Several
    /// small grouped queries rather than one wide projection because the column
    /// set varies per request, and a dynamic projection over a varying column list
    /// is where this would stop being readable — Postgres groups the same set each
    /// time, so the plans are cheap and identical.
    /// </summary>
    private async Task<List<ReportGroupFooterView>> GroupFootersAsync(
        IQueryable<TRow> filtered, IReadOnlyList<string> groupBy, CancellationToken ct)
    {
        Expression<Func<TRow, string>> key =
            ReportQueryBuilder<TRow>.GroupKeySelector(groupBy, Map);

        List<ReportGroupFooterView> footers = [.. (await filtered
            .GroupBy(key)
            .Select(g => new { Key = g.Key, Count = g.LongCount() })
            .ToListAsync(ct))
            .Select(g => new ReportGroupFooterView
            {
                Path = [.. g.Key.Split(ReportQueryBuilder<TRow>.GroupSeparator)],
                RowCount = g.Count,
            })];

        Dictionary<string, ReportGroupFooterView> byKey = footers.ToDictionary(
            f => string.Join(ReportQueryBuilder<TRow>.GroupSeparator, f.Path),
            StringComparer.Ordinal);

        foreach (ReportColumn column in Columns.Where(c => c.IsAggregatable))
        {
            Expression<Func<TRow, decimal?>> value =
                ReportQueryBuilder<TRow>.DecimalSelector(column);

            var aggregated = await filtered
                .GroupBy(key, value)
                .Select(g => new { Key = g.Key, Total = g.Sum() })
                .ToListAsync(ct);

            foreach (var row in aggregated)
            {
                if (byKey.TryGetValue(row.Key, out ReportGroupFooterView? footer))
                {
                    footer.Aggregates[column.Key] = row.Total;
                }
            }
        }

        return footers;
    }

    private List<ReportColumn> Select(IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return [.. Columns];
        }

        List<ReportColumn> selected = [];

        foreach (string key in requested)
        {
            if (!Map.TryGetValue(key, out ReportColumn? column))
            {
                throw new ReportQueryException($"This report has no column '{key}'.");
            }

            selected.Add(column);
        }

        return selected;
    }

    /// <summary>
    /// Row to dictionary, evaluated against the already-materialised row. The
    /// selectors ran in SQL to fetch it; this only reads what came back.
    /// </summary>
    private static Dictionary<string, object?> Project(TRow row, List<ReportColumn> columns)
    {
        Dictionary<string, object?> projected = [];

        foreach (ReportColumn column in columns)
        {
            projected[column.Key] = column.Read(row);
        }

        return projected;
    }
}
