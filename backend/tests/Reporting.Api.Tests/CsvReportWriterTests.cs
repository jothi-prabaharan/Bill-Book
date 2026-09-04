using System.Text;
using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The CSV export, checked by reading back the bytes it produced.
///
/// <b>The interesting cases are the ones a hand-written CSV gets wrong</b>: a
/// field with a comma in it, a field with a quote in it, a field with a newline in
/// it, a non-Latin name, and a money column that has to stay parseable in a branch
/// whose screen format is Indian grouping. Asserting that the file is non-empty
/// would prove none of them.
/// </summary>
public class CsvReportWriterTests
{
    private static string Decode(byte[] bytes)
    {
        // The BOM is deliberate; strip it here so the assertions read as the CSV
        // text rather than as three bytes and the CSV text.
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());

        return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
    }

    private static ReportResultView Sample() => new()
    {
        ReportKey = "account-movement",
        Title = "Account Movement",
        GeneratedAt = DateTimeOffset.UtcNow,
        Currency = new ReportCurrencyView { Code = "INR", Decimals = 2 },
        Columns =
        [
            new() { Key = "date", Header = "Date", DataType = ColumnDataType.Date },
            new() { Key = "account", Header = "Account", DataType = ColumnDataType.Text },
            new() { Key = "debit", Header = "Debit(INR)", DataType = ColumnDataType.Money },
        ],
        Rows =
        [
            new() { ["date"] = new DateOnly(2026, 4, 1), ["account"] = "Cash", ["debit"] = 100.50m },
            new() { ["date"] = new DateOnly(2026, 4, 2), ["account"] = "Bank", ["debit"] = 250m },
        ],
        GroupFooters =
        [
            new() { Path = ["Asset"], RowCount = 2, Aggregates = { ["debit"] = 350.50m } },
        ],
        GrandTotal = new ReportGroupFooterView
        {
            Path = [],
            RowCount = 2,
            Aggregates = { ["debit"] = 350.50m },
        },
    };

    [Fact]
    public void Writes_the_declared_headers_in_their_declared_order()
    {
        string csv = Decode(CsvReportWriter.Write(Sample()));

        Assert.StartsWith("Date,Account,Debit(INR)\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_row_per_result_row_then_the_footers()
    {
        string[] lines = Decode(CsvReportWriter.Write(Sample()))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // header + 2 rows + 1 group footer + grand total
        Assert.Equal(5, lines.Length);
        Assert.Equal("2026-04-01,Cash,100.50", lines[1]);
        Assert.Equal("2026-04-02,Bank,250", lines[2]);
        Assert.Equal("Asset,,350.50", lines[3]);
        Assert.Equal("Total,,350.50", lines[4]);
    }

    [Fact]
    public void Quotes_a_field_containing_a_comma()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["account"] = "Kumar, Ravi & Sons" }];

        Assert.Contains("\"Kumar, Ravi & Sons\"", Decode(CsvReportWriter.Write(result)), StringComparison.Ordinal);
    }

    [Fact]
    public void Doubles_an_embedded_quote_and_wraps_the_field()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["account"] = "The \"Cash\" account" }];

        Assert.Contains(
            "\"The \"\"Cash\"\" account\"",
            Decode(CsvReportWriter.Write(result)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_an_embedded_newline_inside_one_quoted_field()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["account"] = "Line one\nLine two" }];
        result.GroupFooters = [];
        result.GrandTotal = null;

        string csv = Decode(CsvReportWriter.Write(result));

        Assert.Contains("\"Line one\nLine two\"", csv, StringComparison.Ordinal);

        // Header, the one record, and nothing else: the embedded newline must not
        // have split the record into two.
        Assert.Equal(2, CountRecords(csv));
    }

    [Fact]
    public void Preserves_multilingual_text()
    {
        ReportResultView result = Sample();
        result.Rows =
        [
            new() { ["account"] = "ரொக்கம்" },
            new() { ["account"] = "现金账户" },
        ];

        string csv = Decode(CsvReportWriter.Write(result));

        Assert.Contains("ரொக்கம்", csv, StringComparison.Ordinal);
        Assert.Contains("现金账户", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_money_with_a_decimal_point_and_no_grouping()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["debit"] = 1234567.89m }];

        // Indian grouping would render this 12,34,567.89 — which is right on the
        // screen and unparseable in a CSV column.
        Assert.Contains(",1234567.89", Decode(CsvReportWriter.Write(result)), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_null_as_an_empty_field_rather_than_the_word_null()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["date"] = null, ["account"] = null, ["debit"] = null }];

        string[] lines = Decode(CsvReportWriter.Write(result))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(",,", lines[1]);
    }

    [Fact]
    public void Renders_a_boolean_column_as_yes_or_no()
    {
        ReportResultView result = Sample();
        result.Columns = [new() { Key = "active", Header = "Active", DataType = ColumnDataType.Boolean }];
        result.Rows = [new() { ["active"] = true }, new() { ["active"] = false }];
        result.GroupFooters = [];
        result.GrandTotal = null;

        string[] lines = Decode(CsvReportWriter.Write(result))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Yes", lines[1]);
        Assert.Equal("No", lines[2]);
    }

    [Fact]
    public void Writes_dates_as_iso_8601()
    {
        ReportResultView result = Sample();
        result.Columns =
        [
            new() { Key = "d", Header = "D", DataType = ColumnDataType.Date },
            new() { Key = "dt", Header = "DT", DataType = ColumnDataType.DateTime },
        ];
        result.Rows =
        [
            new()
            {
                ["d"] = new DateOnly(2026, 12, 31),
                ["dt"] = new DateTimeOffset(2026, 12, 31, 18, 45, 3, TimeSpan.Zero),
            },
        ];
        result.GroupFooters = [];
        result.GrandTotal = null;

        string[] lines = Decode(CsvReportWriter.Write(result))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("2026-12-31,2026-12-31 18:45:03", lines[1]);
    }

    [Fact]
    public void Writes_only_the_columns_the_result_declares_in_their_order()
    {
        ReportResultView result = Sample();

        // A pivot, or a column chooser, hands the writer a reordered subset. Any
        // value not named by a column must not appear.
        result.Columns =
        [
            new() { Key = "debit", Header = "Debit(INR)", DataType = ColumnDataType.Money },
            new() { Key = "account", Header = "Account", DataType = ColumnDataType.Text },
        ];
        result.Rows =
        [
            new() { ["date"] = new DateOnly(2026, 4, 1), ["account"] = "Cash", ["debit"] = 10m },
        ];
        result.GroupFooters = [];
        result.GrandTotal = null;

        string[] lines = Decode(CsvReportWriter.Write(result))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Debit(INR),Account", lines[0]);
        Assert.Equal("10,Cash", lines[1]);
        Assert.DoesNotContain("2026-04-01", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Quotes_a_field_whose_whitespace_would_otherwise_be_eaten()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["account"] = "  indented" }];

        Assert.Contains("\"  indented\"", Decode(CsvReportWriter.Write(result)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Counts records the way a conforming reader does: a newline inside quotes is
    /// part of the field, not a record separator.
    /// </summary>
    private static int CountRecords(string csv)
    {
        int records = 0;
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            if (csv[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && csv[i] == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
            {
                records++;
                i++;
            }
        }

        return records;
    }
}
