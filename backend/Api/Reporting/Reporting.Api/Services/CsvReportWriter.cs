using System.Globalization;
using System.Text;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;

namespace Reporting.Api.Services;

/// <summary>
/// A report as a .csv.
///
/// <b>It consumes the same <see cref="ReportResultView"/> the API returns, with
/// paging off</b> — the same contract <see cref="ExcelReportWriter"/> takes, and
/// for the same reason: one serializer feeds the screen and both files, so an
/// export cannot disagree with what was on screen when somebody pressed the
/// button. Filters, sorting, grouping, pivot and the selected columns are already
/// applied by the engine before either writer sees the result; neither writer has
/// any query semantics of its own, which is what stops the two exports drifting.
///
/// <b>No package was added for this.</b> RFC 4180 is a quoting rule and a line
/// ending, and a CSV library would earn its dependency only by also parsing.
///
/// The two things a hand-written CSV usually gets wrong, and how this does not:
///
/// <list type="bullet">
/// <item><b>Encoding.</b> Written UTF-8 <i>with</i> a byte-order mark. Tamil and
/// Chinese contact names are the whole reason: Excel on Windows reads a BOM-less
/// UTF-8 file in the machine's ANSI code page and renders every non-Latin name as
/// mojibake, and the person who opens it has no way to tell the file from the
/// reader. The BOM costs three bytes and every other consumer skips it.</item>
/// <item><b>Numbers and dates as text.</b> Money and quantities are written with
/// the invariant culture and no thousands separators, so a decimal point stays a
/// decimal point in a branch whose display format is Indian grouping; dates go out
/// as ISO 8601. A CSV is an interchange format — the grid's formatting belongs on
/// the grid, and re-applying it here is how a column arrives unparseable.</item>
/// </list>
/// </summary>
public static class CsvReportWriter
{
    /// <summary>
    /// <c>text/csv</c> with the charset stated, because the BOM alone does not
    /// stop a browser guessing.
    /// </summary>
    public const string ContentType = "text/csv; charset=utf-8";

    public static byte[] Write(ReportResultView result)
    {
        var builder = new StringBuilder();

        AppendRow(builder, result.Columns.Select(c => c.Header));

        foreach (Dictionary<string, object?> row in result.Rows)
        {
            AppendRow(builder, result.Columns.Select(column =>
            {
                row.TryGetValue(column.Key, out object? value);
                return Render(value, column.DataType);
            }));
        }

        // Subtotals below the detail, exactly as the .xlsx writes them. A footer
        // that only exists on screen is a figure somebody re-derives by hand.
        foreach (ReportGroupFooterView footer in result.GroupFooters)
        {
            AppendFooter(
                builder,
                string.Join(" > ", footer.Path.Select(p => p ?? string.Empty)),
                footer.Aggregates,
                result.Columns);
        }

        if (result.GrandTotal is ReportGroupFooterView total)
        {
            AppendFooter(builder, "Total", total.Aggregates, result.Columns);
        }

        // Encoding.UTF8 rather than new UTF8Encoding(false): the BOM is wanted.
        return Encoding.UTF8.GetPreamble()
            .Concat(new UTF8Encoding(false).GetBytes(builder.ToString()))
            .ToArray();
    }

    private static void AppendFooter(
        StringBuilder builder,
        string label,
        Dictionary<string, decimal?> aggregates,
        List<ReportColumnView> columns)
    {
        AppendRow(builder, columns.Select((column, index) =>
        {
            if (index == 0)
            {
                return label;
            }

            return aggregates.TryGetValue(column.Key, out decimal? value) && value is decimal amount
                ? amount.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }));
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string> fields)
    {
        bool first = true;

        foreach (string field in fields)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(Quote(field));
            first = false;
        }

        // CRLF, which is what RFC 4180 says and what Excel expects. A file with
        // bare LFs opens fine in most readers and badly in the one this is for.
        builder.Append("\r\n");
    }

    /// <summary>
    /// RFC 4180 quoting: a field containing a comma, a quote, a carriage return or
    /// a newline is wrapped in quotes and its own quotes are doubled.
    ///
    /// <b>A leading space is also enough to quote.</b> Some readers strip it and
    /// some do not, and a contact name that loses its indentation in one tool and
    /// keeps it in another is a difference nobody can explain later.
    /// </summary>
    private static string Quote(string field)
    {
        if (field.Length == 0)
        {
            return string.Empty;
        }

        bool needsQuoting =
            field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            || char.IsWhiteSpace(field[0])
            || char.IsWhiteSpace(field[^1]);

        return needsQuoting ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }

    /// <summary>
    /// One value as text.
    ///
    /// Typed by the column's declared type rather than by the runtime type of the
    /// value, so a money column reads the same whether the engine handed back a
    /// <c>decimal</c> or a <c>double</c>.
    /// </summary>
    private static string Render(object? value, ColumnDataType type)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return type switch
        {
            ColumnDataType.Money
                or ColumnDataType.Quantity
                or ColumnDataType.Number
                or ColumnDataType.Percent
                or ColumnDataType.Rate =>
                Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture),

            ColumnDataType.Date => value switch
            {
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset offset => offset.UtcDateTime.ToString(
                    "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime plain => plain.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            },

            ColumnDataType.DateTime => value switch
            {
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset offset => offset.UtcDateTime.ToString(
                    "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime plain => plain.ToString(
                    "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            },

            ColumnDataType.Boolean => (bool)value ? "Yes" : "No",

            _ => value.ToString() ?? string.Empty,
        };
    }
}
