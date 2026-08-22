using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Accounting.Api.Services;

/// <summary>
/// Turns an .xlsx bank statement into the same rows of cells a CSV becomes.
///
/// Three things about the format make a naive read wrong, and all three appear
/// in real bank exports:
///
/// <b>Cells are sparse.</b> A row with nothing in column B has no B element at
/// all — so reading cells in order silently shifts every later column one to the
/// left. The cell reference is what says where a value belongs, and it is read
/// rather than counted.
///
/// <b>Text is usually not in the cell.</b> Most strings live in a shared table
/// and the cell holds an index into it. A reader that takes the cell's own text
/// gets a row of integers where the narration should be.
///
/// <b>Dates are numbers.</b> Excel stores a date as days since 1900 with a leap
/// year that never happened, so the serial is converted here rather than left for
/// the date parser to fail on. A cell already formatted as text is passed through
/// untouched, which is what a bank that exports <c>dd-MMM-yyyy</c> produces.
/// </summary>
public static class ExcelStatementReader
{
    public static StatementGrid Read(Stream stream, int skipRows, bool hasHeaderRow)
    {
        // The package reader needs to seek, and an uploaded form file may not.
        using var seekable = new MemoryStream();
        stream.CopyTo(seekable);
        seekable.Position = 0;

        using SpreadsheetDocument document = OpenOrThrow(seekable);

        WorkbookPart workbook = document.WorkbookPart
            ?? throw new StatementReadException("The workbook is empty.");

        // The first sheet, because a bank statement export has exactly one and a
        // picker for something with one option is a question nobody can answer.
        WorksheetPart sheet = workbook.WorksheetParts.FirstOrDefault()
            ?? throw new StatementReadException("The workbook has no sheets.");

        string[] shared = SharedStrings(workbook);

        // Resolved once. Looked up per cell it would be a walk up the tree
        // and back down the style part for every value in the file.
        CellFormat[] formats = [.. workbook.WorkbookStylesPart?.Stylesheet?.CellFormats
            ?.Elements<CellFormat>() ?? []];

        var all = new List<string[]>();

        Worksheet worksheet = sheet.Worksheet
            ?? throw new StatementReadException("The first sheet has no content.");

        foreach (Row row in worksheet.Descendants<Row>())
        {
            var cells = new List<string>();

            foreach (Cell cell in row.Elements<Cell>())
            {
                // Sparse rows: pad out to where this cell actually sits.
                int index = ColumnIndex(cell.CellReference?.Value);
                while (cells.Count < index)
                {
                    cells.Add(string.Empty);
                }

                cells.Add(ValueOf(cell, shared, formats));
            }

            all.Add([.. cells]);
        }

        all.RemoveAll(row => row.All(string.IsNullOrWhiteSpace));

        if (skipRows > 0)
        {
            all = [.. all.Skip(skipRows)];
        }

        if (all.Count == 0)
        {
            throw new StatementReadException(
                skipRows > 0
                    ? $"There is nothing left after skipping {skipRows} row(s)."
                    : "The sheet has no rows.");
        }

        return hasHeaderRow
            ? new StatementGrid(all[0], [.. all.Skip(1)])
            : new StatementGrid([], all);
    }

    private static SpreadsheetDocument OpenOrThrow(Stream stream)
    {
        try
        {
            return SpreadsheetDocument.Open(stream, isEditable: false);
        }
        catch (Exception ex) when (ex is not StatementReadException)
        {
            // The overwhelmingly common cause: an .xls renamed to .xlsx, or a
            // portal that serves HTML with a spreadsheet's file name.
            throw new StatementReadException(
                "That file could not be read as an Excel workbook. If it came from a bank "
                    + "portal it may be an older .xls or an HTML page with an .xlsx name — "
                    + "open it and save as .xlsx, or export CSV instead.",
                ex);
        }
    }

    private static string[] SharedStrings(WorkbookPart workbook) =>
        workbook.SharedStringTablePart?.SharedStringTable is { } table
            ? [.. table.Elements<SharedStringItem>().Select(i => i.InnerText)]
            : [];

    /// <summary>
    /// Zero-based column from a cell reference — <c>A1</c> is 0, <c>AB7</c> is 27.
    /// Base-26 with no zero, which is why the accumulator starts from one each
    /// letter rather than shifting.
    /// </summary>
    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return 0;
        }

        int index = 0;

        foreach (char c in reference)
        {
            if (!char.IsLetter(c))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return index - 1;
    }

    private static string ValueOf(Cell cell, string[] shared, CellFormat[] formats)
    {
        string raw = cell.CellValue?.InnerText ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(raw, out int index) && index >= 0 && index < shared.Length
                ? shared[index]
                : string.Empty;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InnerText;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }

        // A date-formatted number. Written back in a form the profile's date
        // format can parse, rather than as a serial the parser would reject.
        if (IsDateFormatted(cell, formats)
            && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double serial))
        {
            return FromSerial(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return raw;
    }

    /// <summary>
    /// Whether the cell carries one of Excel's built-in date formats. Only the
    /// built-ins are recognised, because reading custom formats means walking the
    /// style part to find a format string and then deciding whether it looks like
    /// a date — and a wrong guess turns an amount into a date in 1904.
    /// </summary>
    private static bool IsDateFormatted(Cell cell, CellFormat[] formats)
    {
        if (cell.StyleIndex?.Value is not { } styleIndex)
        {
            return false;
        }

        uint? numberFormat = formats.ElementAtOrDefault((int)styleIndex)?.NumberFormatId?.Value;

        // 14–22 are the built-in date and time formats; 45–47 are elapsed time.
        return numberFormat is (>= 14 and <= 22) or (>= 45 and <= 47);
    }

    /// <summary>
    /// Excel's serial to a date. The epoch is 30 December 1899 rather than the
    /// 1st, which absorbs the leap day Excel believes 1900 had — a bug kept since
    /// Lotus 1-2-3 and still load-bearing.
    /// </summary>
    private static DateTime FromSerial(double serial) =>
        new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified).AddDays(serial);
}
