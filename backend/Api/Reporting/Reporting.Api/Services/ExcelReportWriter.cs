using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;

namespace Reporting.Api.Services;

/// <summary>
/// A report as an .xlsx.
///
/// <b>It consumes the same <see cref="ReportResultView"/> the API returns, with
/// paging off.</b> One serializer feeds the screen and the file, so an export
/// cannot disagree with what was on screen when somebody pressed the button —
/// which is the failure that makes people stop trusting both.
///
/// Written on <c>DocumentFormat.OpenXml</c>, already pinned and already carrying
/// the bank-statement import. No package was added for this.
/// </summary>
public static class ExcelReportWriter
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Style indices into the stylesheet built below.
    private const uint StyleDefault = 0;
    private const uint StyleHeader = 1;
    private const uint StyleMoney = 2;
    private const uint StyleDate = 3;
    private const uint StyleQuantity = 4;
    private const uint StyleTotalText = 5;
    private const uint StyleTotalMoney = 6;

    public static byte[] Write(ReportResultView result)
    {
        using var buffer = new MemoryStream();

        using (SpreadsheetDocument document = SpreadsheetDocument.Create(
            buffer, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();

            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = Styles();

            WorksheetPart worksheetPart = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();

            worksheetPart.Worksheet = new Worksheet(
                FrozenHeader(),
                ColumnWidths(result.Columns),
                data);

            workbook.Workbook.AppendChild(new Sheets()).Append(new Sheet
            {
                Id = workbook.GetIdOfPart(worksheetPart),
                SheetId = 1,
                // Excel refuses several characters in a sheet name and truncates at
                // 31. "Invoice/DN Payment Collection Report" is neither legal nor
                // short, so it is cleaned rather than left to fail on open.
                Name = SheetName(result.Title),
            });

            data.Append(HeaderRow(result.Columns));

            foreach (Dictionary<string, object?> row in result.Rows)
            {
                data.Append(BodyRow(row, result.Columns));
            }

            // Group subtotals as real rows, below the detail. A footer that only
            // exists on screen is a figure somebody re-derives by hand in the
            // spreadsheet, usually differently.
            foreach (ReportGroupFooterView footer in result.GroupFooters)
            {
                data.Append(FooterRow(
                    string.Join(" › ", footer.Path.Select(p => p ?? string.Empty)),
                    footer.Aggregates,
                    result.Columns));
            }

            if (result.GrandTotal is ReportGroupFooterView total)
            {
                data.Append(FooterRow("Total", total.Aggregates, result.Columns));
            }

            workbook.Workbook.Save();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Freezes the header row.
    ///
    /// The same promise the grid makes on screen: scroll a thousand rows and the
    /// column you are reading still has a name.
    /// </summary>
    private static SheetViews FrozenHeader() =>
        new(new SheetView
        {
            WorkbookViewId = 0,
            Pane = new Pane
            {
                VerticalSplit = 1,
                TopLeftCell = "A2",
                ActivePane = PaneValues.BottomLeft,
                State = PaneStateValues.Frozen,
            },
        });

    private static Columns ColumnWidths(List<ReportColumnView> columns)
    {
        var element = new Columns();

        for (int index = 0; index < columns.Count; index++)
        {
            element.Append(new Column
            {
                Min = (uint)(index + 1),
                Max = (uint)(index + 1),
                CustomWidth = true,
                // Excel's width unit is characters, not pixels. A column asking for
                // 160px is about 22 characters; without the conversion every column
                // comes out narrow enough to show ####.
                Width = columns[index].Width is int pixels
                    ? Math.Clamp(pixels / 7.0, 8, 60)
                    : DefaultWidth(columns[index].DataType),
            });
        }

        return element;
    }

    private static double DefaultWidth(ColumnDataType type) => type switch
    {
        ColumnDataType.Money or ColumnDataType.Quantity => 16,
        ColumnDataType.Date => 12,
        ColumnDataType.DateTime => 20,
        ColumnDataType.Number => 12,
        _ => 24,
    };

    private static Row HeaderRow(List<ReportColumnView> columns)
    {
        var row = new Row();

        foreach (ReportColumnView column in columns)
        {
            row.Append(TextCell(column.Header, StyleHeader));
        }

        return row;
    }

    private static Row BodyRow(
        Dictionary<string, object?> values, List<ReportColumnView> columns)
    {
        var row = new Row();

        foreach (ReportColumnView column in columns)
        {
            values.TryGetValue(column.Key, out object? value);
            row.Append(CellFor(value, column.DataType));
        }

        return row;
    }

    /// <summary>
    /// A subtotal or the grand total: its label in the first column, its figures
    /// under the columns they belong to, everything else blank.
    /// </summary>
    private static Row FooterRow(
        string label,
        Dictionary<string, decimal?> aggregates,
        List<ReportColumnView> columns)
    {
        var row = new Row();

        for (int index = 0; index < columns.Count; index++)
        {
            ReportColumnView column = columns[index];

            if (index == 0)
            {
                row.Append(TextCell(label, StyleTotalText));
                continue;
            }

            row.Append(aggregates.TryGetValue(column.Key, out decimal? value) && value is decimal amount
                ? NumberCell(amount, StyleTotalMoney)
                : new Cell { StyleIndex = StyleTotalText });
        }

        return row;
    }

    /// <summary>
    /// One cell, typed.
    ///
    /// <b>Numbers are written as numbers.</b> An export whose figures arrive as
    /// text is one somebody re-types before it is any use, and re-typing is where
    /// the digits change.
    /// </summary>
    private static Cell CellFor(object? value, ColumnDataType type)
    {
        if (value is null)
        {
            return new Cell { StyleIndex = StyleDefault };
        }

        return type switch
        {
            ColumnDataType.Money => NumberCell(Convert.ToDecimal(value, CultureInfo.InvariantCulture), StyleMoney),

            ColumnDataType.Quantity =>
                NumberCell(Convert.ToDecimal(value, CultureInfo.InvariantCulture), StyleQuantity),

            ColumnDataType.Number or ColumnDataType.Percent or ColumnDataType.Rate =>
                NumberCell(Convert.ToDecimal(value, CultureInfo.InvariantCulture), StyleDefault),

            // Written as a date serial with a date format, not as text, so the
            // column sorts and filters as dates in the spreadsheet.
            ColumnDataType.Date or ColumnDataType.DateTime => DateCell(value),

            ColumnDataType.Boolean => TextCell(
                (bool)value ? "Yes" : "No", StyleDefault),

            _ => TextCell(value.ToString() ?? string.Empty, StyleDefault),
        };
    }

    private static Cell NumberCell(decimal value, uint style) => new()
    {
        DataType = CellValues.Number,
        CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
        StyleIndex = style,
    };

    private static Cell DateCell(object value)
    {
        DateTime moment = value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset offset => offset.UtcDateTime,
            DateTime plain => plain,
            _ => DateTime.MinValue,
        };

        if (moment == DateTime.MinValue)
        {
            return TextCell(value.ToString() ?? string.Empty, StyleDefault);
        }

        return new Cell
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(
                moment.ToOADate().ToString(CultureInfo.InvariantCulture)),
            StyleIndex = StyleDate,
        };
    }

    /// <summary>
    /// A text cell, written inline.
    ///
    /// Inline strings rather than a shared-string table: a report is written once
    /// and read once, so deduplicating its text buys nothing and costs a second
    /// pass over every row.
    /// </summary>
    private static Cell TextCell(string value, uint style) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value)),
        StyleIndex = style,
    };

    /// <summary>
    /// Excel refuses <c>: \ / ? * [ ]</c> in a sheet name and truncates at 31
    /// characters. Several report titles have a slash in them, and a file that
    /// will not open is a worse export than one with an abbreviated tab.
    /// </summary>
    private static string SheetName(string title)
    {
        string cleaned = new(title.Select(c => ":\\/?*[]".Contains(c) ? '-' : c).ToArray());

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static Stylesheet Styles() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0.00" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "yyyy-mm-dd" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "#,##0.######" }),
        new Fonts(
            new Font(new FontSize { Val = 11 }),
            new Font(new FontSize { Val = 11 }, new Bold())),
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
        new Borders(new Border()),
        new CellFormats(
            // 0 default
            new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },
            // 1 header
            new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true },
            // 2 money
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
            // 3 date
            new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
            // 4 quantity
            new CellFormat { NumberFormatId = 166, ApplyNumberFormat = true },
            // 5 total label
            new CellFormat { FontId = 1, ApplyFont = true },
            // 6 total money
            new CellFormat
            {
                FontId = 1,
                NumberFormatId = 164,
                ApplyFont = true,
                ApplyNumberFormat = true,
            }));
}
