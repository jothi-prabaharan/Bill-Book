using System.Globalization;
using System.Net;
using System.Text;
using Printing.Entity.Enums;
using Printing.Entity.Models;

namespace Printing.Repository.Repositories;

/// <summary>A label's size, as the renderer needs it.</summary>
public sealed record LabelSize(decimal WidthMm, decimal HeightMm, decimal GapMm, int Columns, decimal ColumnGapMm);

/// <summary>
/// Lays labels out as one HTML page the browser prints at the label's exact
/// size (TK-159). Pure.
///
/// <b>On a roll</b> each page is one row of labels — one label, or two or
/// three side by side on a multi-across roll — and the page is exactly that
/// row: <c>@page { size: 50mm 25mm; margin: 0 }</c>. The label printer's own
/// driver knows its media and feeds past the gap between labels, so the gap is
/// not part of the page. <b>On an A4 sticker sheet</b>, the fallback, labels
/// are laid in a grid at their size with the gaps between them, as many
/// across as fit.
///
/// What a label shows is fixed per kind; only its size is the branch's.
/// </summary>
public static class LabelRendererRepository
{
    /// <summary>The most labels one request prints, copies included.</summary>
    public const int MaxLabels = 5000;

    private const decimal SheetWidthMm = 210m;
    private const decimal SheetMarginMm = 8m;

    /// <summary>Labels on the page, copies included.</summary>
    public static int CountOf(IEnumerable<LabelData> labels) => labels.Sum(l => Math.Max(1, l.Copies));

    /// <summary>How many labels fit across an A4 sheet at this size.</summary>
    public static int SheetColumns(LabelSize size) =>
        Math.Max(1, (int)Math.Floor((SheetWidthMm - 2 * SheetMarginMm + size.ColumnGapMm) / (size.WidthMm + size.ColumnGapMm)));

    public static string Render(LabelKind kind, LabelSize size, IReadOnlyList<LabelData> labels, bool sheet)
    {
        List<string> cells = [];
        foreach (LabelData label in labels)
        {
            string cell = Cell(kind, size, label);
            for (int copy = 0; copy < Math.Max(1, label.Copies); copy++)
            {
                cells.Add(cell);
            }
        }

        var body = new StringBuilder();
        if (sheet)
        {
            body.Append("<div class=\"sheet\">").AppendJoin(string.Empty, cells).Append("</div>");
        }
        else
        {
            for (int at = 0; at < cells.Count; at += size.Columns)
            {
                body.Append("<div class=\"row\">").AppendJoin(string.Empty, cells.Skip(at).Take(size.Columns)).Append("</div>");
            }
        }

        return Page(size, sheet, body.ToString());
    }

    private static string Page(LabelSize size, bool sheet, string body)
    {
        decimal rowWidth = size.Columns * size.WidthMm + (size.Columns - 1) * size.ColumnGapMm;
        decimal h = size.HeightMm;

        string pageRule = sheet
            ? $"@page {{ size: A4; margin: {Mm(SheetMarginMm)}; }}"
            : $"@page {{ size: {Mm(rowWidth)} {Mm(h)}; margin: 0; }}";

        string layout = sheet
            ? $".sheet {{ display: grid; grid-template-columns: repeat({SheetColumns(size)}, {Mm(size.WidthMm)}); "
              + $"column-gap: {Mm(size.ColumnGapMm)}; row-gap: {Mm(size.GapMm)}; }}"
              + ".lbl { outline: 0.2mm dashed #bbb; }"
            : $".row {{ display: flex; gap: {Mm(size.ColumnGapMm)}; width: {Mm(rowWidth)}; height: {Mm(h)}; "
              + "overflow: hidden; break-after: page; page-break-after: always; }"
              + ".row:last-child { break-after: auto; page-break-after: auto; }";

        // Text and bars scale with the label's height, so one layout serves a
        // 15 mm jewellery tag and a 40 mm shelf label.
        return "<!doctype html><html><head><meta charset=\"utf-8\"><title>Labels</title><style>"
            + pageRule
            + "html, body { margin: 0; padding: 0; background: #fff; color: #000; }"
            + "body { font-family: Arial, Helvetica, sans-serif; }"
            + layout
            + $".lbl {{ box-sizing: border-box; width: {Mm(size.WidthMm)}; height: {Mm(h)}; padding: {Mm(Clamp(h * 0.06m, 0.8m, 2m))}; "
            + "overflow: hidden; display: flex; flex-direction: column; justify-content: space-between; break-inside: avoid; }"
            + $".lbl .t {{ font-weight: bold; font-size: {Mm(Clamp(h * 0.14m, 1.8m, 5m))}; line-height: 1.1; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}"
            + $".lbl .t2 {{ white-space: normal; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }}"
            + $".lbl .s {{ font-size: {Mm(Clamp(h * 0.1m, 1.5m, 3.2m))}; line-height: 1.15; white-space: nowrap; overflow: hidden; }}"
            + $".lbl .p {{ font-weight: bold; font-size: {Mm(Clamp(h * 0.2m, 2.2m, 8m))}; line-height: 1.05; }}"
            + ".lbl .pt-barcode { display: block; text-align: center; line-height: 1; }"
            + $".lbl .pt-barcode img {{ display: block; height: {Mm(Clamp(h * 0.3m, 3m, 14m))}; max-width: 100%; margin: 0 auto; }}"
            + $".lbl .pt-barcode-text {{ display: block; font: {Mm(Clamp(h * 0.08m, 1.4m, 2.8m))} monospace; }}"
            + "</style></head><body>" + body + "</body></html>";
    }

    /// <summary>One label's markup. Every piece of text is HTML-encoded.</summary>
    public static string Cell(LabelKind kind, LabelSize size, LabelData label)
    {
        string code = (label.Barcode ?? label.ItemCode ?? string.Empty).Trim();
        string barcode = BarcodeImagesRepository.Img(code);
        var lines = new StringBuilder();

        switch (kind)
        {
            case LabelKind.PriceTag:
                lines.Append(Div("t", label.Name));
                if (label.Price is decimal price)
                {
                    lines.Append(Div("p", Money(price)));
                }

                if (label.Mrp is decimal mrp && mrp != label.Price)
                {
                    lines.Append(Div("s", "MRP " + Money(mrp)));
                }

                break;

            case LabelKind.ShelfLabel:
                lines.Append(Div("t t2", label.Name));
                if (!string.IsNullOrWhiteSpace(label.ItemCode))
                {
                    lines.Append(Div("s", label.ItemCode));
                }

                if (label.Price is decimal shelfPrice)
                {
                    lines.Append(Div("p", Money(shelfPrice)));
                }

                break;

            case LabelKind.BatchExpiry:
                lines.Append(Div("t", label.Name));
                lines.Append(Div("s", Join("  ",
                    label.BatchNo is { Length: > 0 } batch ? "Batch " + batch : null,
                    label.ManufactureDate is DateOnly mfd ? "Mfd " + MonthYear(mfd) : null,
                    label.ExpiryDate is DateOnly exp ? "Exp " + MonthYear(exp) : null)));
                if (label.Mrp is decimal batchMrp)
                {
                    lines.Append(Div("s", "MRP " + Money(batchMrp)));
                }

                break;

            case LabelKind.JewelleryTag:
                lines.Append(Div("t", label.ItemCode is { Length: > 0 } itemCode ? itemCode : label.Name));
                lines.Append(Div("s", Join("  ",
                    label.GrossWeightGrams is decimal gross ? "G.Wt " + Grams(gross) : null,
                    label.NetWeightGrams is decimal net ? "N.Wt " + Grams(net) : null)));
                lines.Append(Div("s", Join("  ",
                    label.Purity,
                    label.Huid is { Length: > 0 } huid ? "HUID " + huid : null)));
                break;
        }

        if (!string.IsNullOrWhiteSpace(label.SerialNo))
        {
            lines.Append(Div("s", "S/N " + label.SerialNo));
        }

        return $"<div class=\"lbl\"><div>{lines}</div>{barcode}</div>";
    }

    private static string Div(string css, string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : $"<div class=\"{css}\">{WebUtility.HtmlEncode(text.Trim())}</div>";

    private static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>Rupees with the sign and two places, no grouping: a label has no room for commas.</summary>
    public static string Money(decimal amount) => "₹" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Grams(decimal grams) => grams.ToString("0.000", CultureInfo.InvariantCulture) + " g";

    private static string MonthYear(DateOnly date) => date.ToString("MM/yyyy", CultureInfo.InvariantCulture);

    private static string Mm(decimal mm) => mm.ToString("0.##", CultureInfo.InvariantCulture) + "mm";

    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Min(max, Math.Max(min, value));
}
