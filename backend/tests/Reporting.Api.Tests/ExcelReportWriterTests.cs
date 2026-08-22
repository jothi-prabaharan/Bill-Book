using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Excel export, checked by opening the file it produces and reading it back.
///
/// <b>Asserting on the byte count would prove only that something was written.</b>
/// What matters is that a spreadsheet can total the money column — which needs the
/// figures to be numbers rather than text — that the header is frozen, and that
/// the subtotals arrive as rows rather than as something only the screen had.
/// </summary>
public class ExcelReportWriterTests
{
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
            RowCount = 2,
            Aggregates = { ["debit"] = 350.50m },
        },
    };

    private static SheetData Read(byte[] file)
    {
        using var buffer = new MemoryStream(file);
        using SpreadsheetDocument document = SpreadsheetDocument.Open(buffer, false);

        return Sheet(document).GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("The workbook has no sheet data.");
    }

    /// <summary>The single worksheet, asserted into existence rather than suppressed.</summary>
    private static Worksheet Sheet(SpreadsheetDocument document)
    {
        WorkbookPart workbook = document.WorkbookPart
            ?? throw new InvalidOperationException("The file is not a workbook.");

        return workbook.WorksheetParts.Single().Worksheet
            ?? throw new InvalidOperationException("The workbook part has no worksheet.");
    }

    [Fact]
    public void The_file_opens_as_a_workbook()
    {
        // The cheapest real check there is: a corrupt package throws here rather
        // than in front of whoever opened the download.
        Assert.NotNull(Read(ExcelReportWriter.Write(Sample())));
    }

    [Fact]
    public void Every_row_is_written_beneath_a_header()
    {
        List<Row> rows = [.. Read(ExcelReportWriter.Write(Sample())).Elements<Row>()];

        // header + 2 detail + 1 group footer + 1 grand total
        Assert.Equal(5, rows.Count);
        Assert.Equal(
            "Debit(INR)",
            rows[0].Elements<Cell>().Last().InnerText);
    }

    [Fact]
    public void Money_is_written_as_a_number_so_a_spreadsheet_can_total_it()
    {
        List<Row> rows = [.. Read(ExcelReportWriter.Write(Sample())).Elements<Row>()];
        Cell debit = rows[1].Elements<Cell>().Last();

        Assert.Equal(CellValues.Number, debit.DataType?.Value);
        Assert.Equal("100.50", debit.CellValue?.InnerText);
    }

    [Fact]
    public void Dates_are_written_as_date_serials_rather_than_text()
    {
        // So the column sorts and filters as dates in the spreadsheet. Text dates
        // sort "2026-04-10" before "2026-04-2", which nobody notices until a report
        // is sorted by date and the order is wrong.
        Cell date = Read(ExcelReportWriter.Write(Sample()))
            .Elements<Row>().ElementAt(1).Elements<Cell>().First();

        Assert.Equal(CellValues.Number, date.DataType?.Value);
        Assert.Equal(
            new DateTime(2026, 4, 1).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture),
            date.CellValue?.InnerText);
    }

    [Fact]
    public void Subtotals_and_the_grand_total_arrive_as_rows()
    {
        List<Row> rows = [.. Read(ExcelReportWriter.Write(Sample())).Elements<Row>()];

        Assert.Equal("Asset", rows[3].Elements<Cell>().First().InnerText);
        Assert.Equal("350.50", rows[3].Elements<Cell>().Last().CellValue?.InnerText);

        Assert.Equal("Total", rows[4].Elements<Cell>().First().InnerText);
        Assert.Equal("350.50", rows[4].Elements<Cell>().Last().CellValue?.InnerText);
    }

    [Fact]
    public void The_header_row_is_frozen()
    {
        using var buffer = new MemoryStream(ExcelReportWriter.Write(Sample()));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(buffer, false);

        Pane? pane = Sheet(document).GetFirstChild<SheetViews>()
            ?.GetFirstChild<SheetView>()?.Pane;

        Assert.NotNull(pane);
        Assert.Equal(1D, pane.VerticalSplit?.Value);
        Assert.Equal(PaneStateValues.Frozen, pane.State?.Value);
    }

    [Fact]
    public void A_null_value_becomes_an_empty_cell_rather_than_the_word_null()
    {
        ReportResultView result = Sample();
        result.Rows = [new() { ["date"] = null, ["account"] = null, ["debit"] = null }];
        result.GroupFooters = [];
        result.GrandTotal = null;

        Row row = Read(ExcelReportWriter.Write(result)).Elements<Row>().ElementAt(1);

        Assert.All(row.Elements<Cell>(), cell => Assert.Empty(cell.InnerText));
    }

    [Fact]
    public void A_title_that_excel_would_refuse_is_cleaned_rather_than_left_to_fail()
    {
        // "Invoice/DN Payment Collection Report" is both illegal and too long, and
        // a workbook that will not open is a worse export than an abbreviated tab.
        ReportResultView result = Sample();
        result.Title = "Invoice/DN Payment Collection Report";

        using var buffer = new MemoryStream(ExcelReportWriter.Write(result));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(buffer, false);

        WorkbookPart workbook = document.WorkbookPart
            ?? throw new InvalidOperationException("The file is not a workbook.");

        Sheets sheets = workbook.Workbook?.Sheets
            ?? throw new InvalidOperationException("The workbook has no sheets.");

        string? name = sheets.Elements<Sheet>().Single().Name?.Value;
        Assert.NotNull(name);

        Assert.DoesNotContain('/', name);
        Assert.True(name.Length <= 31);
    }
}
