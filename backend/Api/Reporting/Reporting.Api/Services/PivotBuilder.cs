using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;

namespace Reporting.Api.Services;

/// <summary>
/// A pivot: rows down the side, columns across the top, aggregates in the cells.
///
/// <b>Grouped and aggregated in SQL; transposed in memory.</b> Only the second half
/// is not translated, and it operates on the aggregated result — hundreds of rows,
/// not millions — so the database still does all the work proportional to the data.
///
/// <b>The column axis is data-dependent, which is what makes a pivot different from
/// grouping.</b> Pivoting sales by month yields as many columns as there are months
/// with data, so the result declares its columns and the grid renders whatever it
/// is told rather than what it expected.
/// </summary>
public static class PivotBuilder<TRow>
    where TRow : class
{
    /// <summary>
    /// The ceiling on the column axis.
    ///
    /// A pivot by contact across a few thousand contacts is not a report, it is a
    /// spreadsheet nobody can read and a query nobody wants to run. Refused by
    /// name so the person can pick a different field rather than watching it hang.
    /// </summary>
    public const int MaxColumns = 200;

    public static async Task<ReportResultView> BuildAsync(
        IQueryable<TRow> filtered,
        ReportPivotModel pivot,
        IReadOnlyDictionary<string, ReportColumn> columns,
        CancellationToken ct)
    {
        if (pivot.Rows.Count == 0)
        {
            throw new ReportQueryException("A pivot needs at least one row field.");
        }

        if (pivot.Values.Count == 0)
        {
            throw new ReportQueryException("A pivot needs at least one value field.");
        }

        var rowKey = ReportQueryBuilder<TRow>.GroupKeySelector(pivot.Rows, columns);

        // No column axis is a legitimate pivot: rows and values alone is a summary
        // table, which is what somebody asked for when they picked no column field.
        var columnKey = pivot.Columns.Count > 0
            ? ReportQueryBuilder<TRow>.GroupKeySelector(pivot.Columns, columns)
            : null;

        List<string> columnHeadings = columnKey is null
            ? [string.Empty]
            : await DistinctColumnsAsync(filtered, columnKey, pivot, ct);

        // cell[rowKey][columnKey + valueKey] — assembled from one aggregate query
        // per value field, which is the same shape the group footers use.
        Dictionary<string, Dictionary<string, decimal?>> cells = [];
        List<string> rowKeys = [];

        foreach (ReportPivotValueModel value in pivot.Values)
        {
            ReportColumn column = Column(value.Column, columns);
            var amount = ReportQueryBuilder<TRow>.DecimalSelector(column);

            // One composite key rather than a two-member key object: a string
            // concatenation translates everywhere, while a member-init key is at
            // the provider's mercy — and both halves are split apart again below.
            var key = columnKey is null
                ? rowKey
                : ReportQueryBuilder<TRow>.CombineKeys(rowKey, columnKey);

            var aggregated = await filtered
                .GroupBy(key, amount)
                .Select(g => new { Key = g.Key, Total = g.Sum() })
                .ToListAsync(ct);

            foreach (var entry in aggregated)
            {
                string[] axes = entry.Key.Split(ReportQueryBuilder<TRow>.AxisSeparator);
                string rowPart = axes[0];
                string columnPart = axes.Length > 1 ? axes[1] : string.Empty;

                if (!cells.TryGetValue(rowPart, out Dictionary<string, decimal?>? row))
                {
                    row = [];
                    cells[rowPart] = row;
                    rowKeys.Add(rowPart);
                }

                row[CellKey(columnPart, value.Column)] = entry.Total;
            }
        }

        rowKeys.Sort(StringComparer.Ordinal);

        return new ReportResultView
        {
            Columns = Headings(pivot, columns, columnHeadings),
            Rows = [.. rowKeys.Select(key => Row(key, pivot, columnHeadings, cells[key]))],
            Page = new ReportPageView { Number = 1, Size = rowKeys.Count, TotalRows = rowKeys.Count, TotalPages = 1 },
        };
    }

    /// <summary>
    /// The distinct values on the column axis, in order, refused past the ceiling.
    ///
    /// Counted before they are fetched: asking Postgres how many there are is
    /// cheap, and pulling five thousand back to find out there are five thousand
    /// is not.
    /// </summary>
    private static async Task<List<string>> DistinctColumnsAsync(
        IQueryable<TRow> filtered,
        System.Linq.Expressions.Expression<Func<TRow, string>> columnKey,
        ReportPivotModel pivot,
        CancellationToken ct)
    {
        IQueryable<string> distinct = filtered.Select(columnKey).Distinct();

        int count = await distinct.CountAsync(ct);

        if (count > MaxColumns)
        {
            throw new ReportQueryException(
                $"Pivoting by {string.Join(" and ", pivot.Columns)} would produce {count:N0} "
                + $"columns, and the limit is {MaxColumns}. Pick a field with fewer values.");
        }

        List<string> headings = await distinct.ToListAsync(ct);
        headings.Sort(StringComparer.Ordinal);

        return headings;
    }

    private static List<ReportColumnView> Headings(
        ReportPivotModel pivot,
        IReadOnlyDictionary<string, ReportColumn> columns,
        List<string> columnHeadings)
    {
        List<ReportColumnView> headings =
        [
            // The row axis, as one column. Its levels are joined in the cell
            // rather than split across columns, because a pivot's left edge is
            // read as one label.
            new()
            {
                Key = "pivotRow",
                Header = string.Join(" › ", pivot.Rows.Select(key => Column(key, columns).Key)),
                DataType = ColumnDataType.Text,
                Alignment = ColumnAlignment.Left,
                IsPrimary = true,
            },
        ];

        foreach (string heading in columnHeadings)
        {
            foreach (ReportPivotValueModel value in pivot.Values)
            {
                ReportColumn column = Column(value.Column, columns);

                headings.Add(new ReportColumnView
                {
                    Key = CellKey(heading, value.Column),
                    // With one value field the column axis speaks for itself; with
                    // several, each needs saying which measure it is.
                    Header = pivot.Values.Count == 1
                        ? Display(heading)
                        : $"{Display(heading)} — {column.Key}",
                    DataType = column.DataType,
                    Alignment = ColumnAlignment.Right,
                    Aggregate = value.Aggregate,
                });
            }
        }

        return headings;
    }

    private static Dictionary<string, object?> Row(
        string rowKey,
        ReportPivotModel pivot,
        List<string> columnHeadings,
        Dictionary<string, decimal?> cells)
    {
        Dictionary<string, object?> row = new()
        {
            ["pivotRow"] = string.Join(
                " › ", rowKey.Split(ReportQueryBuilder<TRow>.GroupSeparator).Select(Display)),
        };

        // Every declared column is present, empty ones included. A missing key
        // would leave the grid rendering a ragged row and misaligning the rest.
        foreach (string heading in columnHeadings)
        {
            foreach (ReportPivotValueModel value in pivot.Values)
            {
                string key = CellKey(heading, value.Column);
                row[key] = cells.TryGetValue(key, out decimal? amount) ? amount : null;
            }
        }

        return row;
    }

    private static string CellKey(string columnHeading, string valueColumn) =>
        $"{columnHeading}|{valueColumn}";

    /// <summary>An empty group value reads as "(none)" rather than as a blank heading.</summary>
    private static string Display(string value) =>
        value.Length == 0 ? "(none)" : value;

    private static ReportColumn Column(
        string key, IReadOnlyDictionary<string, ReportColumn> columns) =>
        columns.TryGetValue(key, out ReportColumn? column)
            ? column
            : throw new ReportQueryException($"This report has no column '{key}'.");
}
