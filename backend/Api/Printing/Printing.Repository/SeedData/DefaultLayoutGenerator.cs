using System.Text;
using Printing.Entity.Models;
using Shared.Kernel.Printing;

namespace Printing.Repository.SeedData;

/// <summary>
/// Builds a document type's starting layout from its
/// <see cref="DocumentTypeProfile"/>.
///
/// <b>Generated per branch, never copied from another one.</b> Copying would
/// carry one customer's letterhead into another's books the first time somebody
/// used the wrong source row.
///
/// Every row it produces is stamped with <see cref="SeedVersion"/>, so a later
/// change here can be offered as "reset to latest" against templates that have
/// since been edited, instead of silently overwriting them.
/// </summary>
public static class DefaultLayoutGenerator
{
    /// <summary>
    /// The generation of these layouts. <b>Bump it whenever the markup below
    /// changes</b> — a template stamped with an older value is one a customer
    /// can be offered a reset for.
    ///
    /// <b>2</b> — placeholders became <c>{{Tag}}</c> text rather than a chip
    /// element. A template still stamped 1 carries the old markup and will not
    /// merge; CanResetToLatest is what surfaces that.
    ///
    /// <b>3</b> — every document carries a barcode of its number under the date
    /// and a QR code of it in the signature row (TK-157). A template still at 2
    /// and exactly as generated is upgraded by the seeder; an edited one is
    /// left alone and offered the reset.
    ///
    /// <b>4</b> — an invoice carries the UPI payment QR under its amount in words
    /// (TK-158).
    /// </summary>
    public const int SeedVersion = 4;

    /// <summary>
    /// The oldest generation <see cref="Build(DocumentTypeProfile, int)"/> can
    /// reproduce, and so the oldest an untouched template can be recognised at
    /// and upgraded from. Generation 1 used a different placeholder markup.
    /// </summary>
    public const int OldestReproducibleSeedVersion = 2;

    /// <summary>
    /// A placeholder, as it is stored: <c>{{Tag}}</c> and nothing else.
    ///
    /// <b>No wrapper element.</b> The editor draws a chip around this on load
    /// and writes it back as plain text on save, so nothing about merging
    /// depends on an attribute surviving a sanitiser. Whether a tag repeats is
    /// read from the catalogue's declared Kind, not from a marker in the text —
    /// which is also why there is no longer a ↻ to preserve.
    /// </summary>
    public static string Placeholder(string tag) => $"{{{{{tag}}}}}";

    /// <summary>The same, when the definition is already in hand.</summary>
    public static string Placeholder(PlaceholderDefinition placeholder) => Placeholder(placeholder.Tag);

    /// <summary>The current layout for a document type.</summary>
    public static PrintContent Build(DocumentTypeProfile profile) => Build(profile, SeedVersion);

    /// <summary>
    /// The layout exactly as a given generation wrote it, from
    /// <see cref="OldestReproducibleSeedVersion"/> to <see cref="SeedVersion"/>,
    /// which is how the seeder recognises a template nobody has edited: 3 added
    /// the document codes (TK-157), 4 the UPI QR on an invoice (TK-158).
    ///
    /// A kitchen ticket carries neither: it is read at the pass and thrown
    /// away, and nothing scans it.
    /// </summary>
    public static PrintContent Build(DocumentTypeProfile profile, int generation)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, OldestReproducibleSeedVersion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(generation, SeedVersion);

        if (IsKitchenTicket(profile))
        {
            return KitchenTicket();
        }

        bool withCodes = generation >= 3;
        bool withUpi = generation >= 4 && string.Equals(profile.Code, "INV", StringComparison.OrdinalIgnoreCase);

        return new PrintContent
        {
            FixedHeaderHtml = FixedHeader(),
            HeaderHtml = Header(profile, withCodes),
            DetailsHtml = Details(profile),
            FooterHtml = Footer(profile, withUpi),
            FixedFooterHtml = FixedFooter(withCodes),
        };
    }

    /// <summary>Whether a document type's generated layout carries the codes (all but the kitchen ticket).</summary>
    public static bool CarriesCodes(DocumentTypeProfile profile) => !IsKitchenTicket(profile);

    public static PrintSettings BuildSettings() => new();

    /// <summary>A document type's starting page: an 80 mm thermal roll for a kitchen ticket, the A4 default for the rest.</summary>
    public static PrintSettings BuildSettings(DocumentTypeProfile profile) =>
        IsKitchenTicket(profile)
            ? new PrintSettings
            {
                PrinterType = Printing.Entity.Enums.PrinterType.Thermal,
                RollWidthMm = 80d,
                MarginTopMm = 3d,
                MarginBottomMm = 3d,
                MarginLeftMm = 3d,
                MarginRightMm = 3d,
            }
            : BuildSettings();

    private static bool IsKitchenTicket(DocumentTypeProfile profile) =>
        string.Equals(profile.Code, PlaceholderCatalog.KitchenTicketType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A kitchen ticket (TK-147): the station and table large, then each dish
    /// with its quantity, choices and note. No letterhead and no prices — the
    /// kitchen needs neither, and a narrow roll has no room for them.
    /// </summary>
    private static PrintContent KitchenTicket() => new()
    {
        FixedHeaderHtml = string.Empty,
        HeaderHtml =
            $"<div align=\"center\" style=\"font-weight: bold\">{Placeholder("Kitchen.Copy")}</div>"
            + $"<div align=\"center\" style=\"font-weight: bold; font-size: 16pt\">{Placeholder("Kitchen.Station")} · {Placeholder("Kitchen.Table")}</div>"
            + "<table width=\"100%\"><tbody><tr>"
            + $"<td align=\"left\">{Placeholder("Document.No")}</td>"
            + $"<td align=\"right\">{Placeholder("Kitchen.Time")}</td>"
            + "</tr><tr>"
            + $"<td align=\"left\">{Placeholder("Kitchen.OrderType")} · {Placeholder("Kitchen.OrderNo")}</td>"
            + $"<td align=\"right\">Guests {Placeholder("Kitchen.Guests")}</td>"
            + "</tr></tbody></table>",
        DetailsHtml =
            "<table width=\"100%\"><thead><tr><th align=\"left\">Qty</th><th align=\"left\">Dish</th></tr></thead><tbody><tr>"
            + $"<td align=\"left\" style=\"font-weight: bold; font-size: 12pt\">{Placeholder("Item.Quantity")}</td>"
            + $"<td align=\"left\"><div style=\"font-weight: bold; font-size: 12pt\">{Placeholder("Item.ItemName")}</div><div>{Placeholder("Item.Description")}</div></td>"
            + "</tr></tbody></table>",
        FooterHtml = $"<div>Waiter: {Placeholder("Kitchen.Waiter")}</div>",
        FixedFooterHtml = string.Empty,
    };

    /// <summary>The letterhead. Repeats on every page, which is what makes page two recognisable.</summary>
    private static string FixedHeader() =>
        "<table width=\"100%\"><tbody><tr>"
        + $"<td width=\"20%\">{Placeholder("Organization.Logo")}</td>"
        + "<td align=\"left\">"
        + $"<div style=\"font-weight: bold\">{Placeholder("Organization.Name")}</div>"
        + $"<div>{Placeholder("Organization.Address")}</div>"
        + $"<div>GSTIN: {Placeholder("Organization.Gstin")}</div>"
        + "</td>"
        + "<td align=\"right\">"
        + $"<div>{Placeholder("Organization.Phone")}</div>"
        + $"<div>{Placeholder("Organization.Email")}</div>"
        + "</td>"
        + "</tr></tbody></table>";

    /// <summary>Title, document identity, and who it is addressed to.</summary>
    private static string Header(DocumentTypeProfile profile, bool withCodes)
    {
        var html = new StringBuilder();

        html.Append($"<div align=\"center\" style=\"font-weight: bold\">{profile.Name}</div>");
        html.Append("<table width=\"100%\"><tbody><tr>");

        if (profile.Party != PartyKind.None)
        {
            string label = profile.Party == PartyKind.Customer ? "Bill to" : "Vendor";
            html.Append("<td align=\"left\" width=\"55%\">");
            html.Append($"<div>{label}</div>");
            html.Append($"<div style=\"font-weight: bold\">{Placeholder("Party.Name")}</div>");
            html.Append($"<div>{Placeholder("Party.Address")}</div>");
            html.Append($"<div>GSTIN: {Placeholder("Party.Gstin")}</div>");
            html.Append("</td>");
        }

        html.Append("<td align=\"right\">");
        html.Append($"<div>No: {Placeholder("Document.No")}</div>");
        html.Append($"<div>Date: {Placeholder("Document.Date")}</div>");

        if (profile.HasTax)
        {
            html.Append($"<div>Place of supply: {Placeholder("Document.PlaceOfSupply")}</div>");
        }

        // Scanned to open the document (TK-157, TK-162).
        if (withCodes)
        {
            html.Append($"<div>{Placeholder(PlaceholderCatalog.DocumentBarcodeTag)}</div>");
        }

        html.Append("</td></tr></tbody></table>");
        return html.ToString();
    }

    /// <summary>
    /// The repeating block. Exactly one row carries the list chips, and it is
    /// the innermost row — the renderer repeats that row and nothing above it.
    /// </summary>
    private static string Details(DocumentTypeProfile profile) => profile.Lines switch
    {
        LineShape.Item => Rows(
            ["#", "Description", "HSN/SAC", "Qty", "Rate", "Amount"],
            [
                Placeholder("Item.SlNo"),
                Placeholder("Item.ItemName"),
                Placeholder("Item.HsnSac"),
                Placeholder("Item.Quantity"),
                Placeholder("Item.Rate"),
                Placeholder("Item.Amount"),
            ],
            ["left", "left", "left", "right", "right", "right"]),

        LineShape.Allocation => Rows(
            ["Document", "Date", "Document total", "Applied"],
            [
                Placeholder("Alloc.DocumentNo"),
                Placeholder("Alloc.DocumentDate"),
                Placeholder("Alloc.DocumentTotal"),
                Placeholder("Alloc.Amount"),
            ],
            ["left", "left", "right", "right"]),

        LineShape.Ledger => Rows(
            ["Account", "Narration", "Debit", "Credit"],
            [
                Placeholder("Line.AccountName"),
                Placeholder("Line.Description"),
                Placeholder("Line.Debit"),
                Placeholder("Line.Credit"),
            ],
            ["left", "left", "right", "right"]),

        _ => string.Empty,
    };

    private static string Rows(string[] headings, string[] cells, string[] alignments)
    {
        var html = new StringBuilder("<table width=\"100%\"><thead><tr>");

        for (int i = 0; i < headings.Length; i++)
        {
            html.Append($"<th align=\"{alignments[i]}\">{headings[i]}</th>");
        }

        html.Append("</tr></thead><tbody><tr>");

        for (int i = 0; i < cells.Length; i++)
        {
            html.Append($"<td align=\"{alignments[i]}\">{cells[i]}</td>");
        }

        return html.Append("</tr></tbody></table>").ToString();
    }

    /// <summary>
    /// Tax summary beside the totals — and note the shape.
    ///
    /// <b>The tax table's rows repeat; the row holding both tables does not.</b>
    /// That outer row contains list chips, but only inside a nested table, and
    /// treating it as a repeat candidate is exactly the bug that once printed
    /// the totals block once per item line. The default layout is built this way
    /// on purpose, so the common case exercises the rule rather than the corner
    /// case being the only thing that ever does.
    /// </summary>
    private static string Footer(DocumentTypeProfile profile, bool withUpi)
    {
        var html = new StringBuilder("<table width=\"100%\"><tbody><tr>");

        html.Append("<td width=\"55%\" align=\"left\">");
        if (profile.HasTax)
        {
            html.Append("<table width=\"100%\"><thead><tr>")
                .Append("<th align=\"left\">Tax</th><th align=\"right\">Rate</th>")
                .Append("<th align=\"right\">Taxable</th><th align=\"right\">Amount</th>")
                .Append("</tr></thead><tbody><tr>")
                .Append($"<td align=\"left\">{Placeholder("Tax.Component")}</td>")
                .Append($"<td align=\"right\">{Placeholder("Tax.Rate")}</td>")
                .Append($"<td align=\"right\">{Placeholder("Tax.TaxableValue")}</td>")
                .Append($"<td align=\"right\">{Placeholder("Tax.Amount")}</td>")
                .Append("</tr></tbody></table>");
        }

        html.Append("</td>");

        html.Append("<td align=\"right\"><table width=\"100%\"><tbody>");
        if (profile.HasTotals)
        {
            html.Append(TotalRow("Subtotal", "Totals.SubTotal"));
            if (profile.HasTax)
            {
                html.Append(TotalRow("Tax", "Totals.Tax"));
            }

            html.Append(TotalRow("Round off", "Totals.RoundOff"));
            html.Append("<tr><td align=\"left\" style=\"font-weight: bold\">Total</td>")
                .Append($"<td align=\"right\" style=\"font-weight: bold\">{Placeholder("Totals.GrandTotal")}</td></tr>");
        }

        if (profile.HasPayments)
        {
            html.Append(TotalRow("Paid", "Totals.Paid"));
            html.Append(TotalRow("Outstanding", "Totals.Outstanding"));
        }

        html.Append("</tbody></table></td></tr></tbody></table>");

        if (profile.HasTotals)
        {
            html.Append($"<div>Amount in words: {Placeholder("Totals.AmountInWords")}</div>");
        }

        // Prints nothing once the invoice is paid or when the branch has no UPI id (TK-158).
        if (withUpi)
        {
            html.Append($"<div>{Placeholder(PlaceholderCatalog.UpiQrTag)}</div>");
        }

        return html.ToString();
    }

    private static string TotalRow(string label, string tag) =>
        $"<tr><td align=\"left\">{label}</td><td align=\"right\">{Placeholder(tag)}</td></tr>";

    /// <summary>
    /// Terms and the signature block, with the document's QR code in the
    /// otherwise empty cell beside the signature (TK-157). Repeats on every page.
    /// </summary>
    private static string FixedFooter(bool withCodes) =>
        $"<div>{Placeholder("Document.Terms")}</div>"
        + "<table width=\"100%\"><tbody><tr>"
        + (withCodes ? $"<td align=\"left\">{Placeholder(PlaceholderCatalog.DocumentQrTag)}</td>" : "<td align=\"left\">&nbsp;</td>")
        + $"<td align=\"right\">For {Placeholder("Organization.Name")}</td>"
        + "</tr></tbody></table>";
}
