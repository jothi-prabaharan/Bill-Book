using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Banking.Api.Services;

/// <summary>
/// A statement shaped for export: a name, a header row, and the rows beneath it.
///
/// One shape for both formats, built once by the service, so CSV and Excel cannot
/// drift into exporting different columns — which is the failure that makes
/// people stop trusting either.
/// </summary>
public sealed record StatementExport(
    string Name, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows);

/// <summary>
/// Writes an export to CSV or .xlsx.
///
/// <b>What comes out is meant to be opened, not re-imported.</b> It carries the
/// reconciliation — status, matched document, note — beside what the bank said,
/// which is what somebody sends an accountant or attaches to a query. Re-importing
/// it would be importing the organization's own opinion back as the bank's.
/// </summary>
public static class StatementExportWriter
{
    public const string CsvContentType = "text/csv";

    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] ToCsv(StatementExport export)
    {
        var text = new StringBuilder();

        text.AppendLine(string.Join(',', export.Columns.Select(Quote)));

        foreach (string[] row in export.Rows)
        {
            text.AppendLine(string.Join(',', row.Select(Quote)));
        }

        // A byte-order mark, deliberately. Excel opens a UTF-8 CSV as the system
        // code page without one, which turns every Tamil or Chinese contact name
        // in the narration into mojibake — and the file looks broken rather than
        // the setting that opened it.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    public static byte[] ToExcel(StatementExport export)
    {
        using var buffer = new MemoryStream();

        using (SpreadsheetDocument document = SpreadsheetDocument.Create(
            buffer, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();

            WorksheetPart worksheetPart = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            worksheetPart.Worksheet = new Worksheet(data);

            var sheets = workbook.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbook.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Statement",
            });

            data.Append(RowOf(export.Columns));

            foreach (string[] row in export.Rows)
            {
                data.Append(RowOf(row));
            }

            workbook.Workbook.Save();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// One row. Numbers are written as numbers so a spreadsheet can total the
    /// amount columns — an export whose figures arrive as text is one somebody
    /// has to re-type before it is any use.
    /// </summary>
    private static Row RowOf(IReadOnlyList<string> values)
    {
        var row = new Row();

        foreach (string value in values)
        {
            bool numeric = value.Length > 0
                && decimal.TryParse(
                    value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

            row.Append(numeric
                ? new Cell { DataType = CellValues.Number, CellValue = new CellValue(value) }
                : new Cell
                {
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(value)),
                });
        }

        return row;
    }

    /// <summary>
    /// RFC 4180 quoting, and one thing beyond it: a cell starting with
    /// <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> is prefixed with an apostrophe.
    ///
    /// <b>That is a security fix, not a formatting nicety.</b> A bank narration is
    /// attacker-influenced — anyone who can send the organization money can choose
    /// what appears in it — and a spreadsheet opening this file would execute
    /// <c>=cmd|...</c> as a formula. The apostrophe makes Excel treat it as text.
    /// </summary>
    private static string Quote(string value)
    {
        string safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;

        return safe.Contains(',') || safe.Contains('"') || safe.Contains('\n') || safe.Contains('\r')
            ? $"\"{safe.Replace("\"", "\"\"")}\""
            : safe;
    }
}
