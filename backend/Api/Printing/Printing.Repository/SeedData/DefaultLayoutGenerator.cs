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
    /// </summary>
    public const int SeedVersion = 2;

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

    public static PrintContent Build(DocumentTypeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new PrintContent
        {
            FixedHeaderHtml = FixedHeader(),
            HeaderHtml = Header(profile),
            DetailsHtml = Details(profile),
            FooterHtml = Footer(profile),
            FixedFooterHtml = FixedFooter(),
        };
    }

    public static PrintSettings BuildSettings() => new();

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
    private static string Header(DocumentTypeProfile profile)
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
    private static string Footer(DocumentTypeProfile profile)
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

        return html.ToString();
    }

    private static string TotalRow(string label, string tag) =>
        $"<tr><td align=\"left\">{label}</td><td align=\"right\">{Placeholder(tag)}</td></tr>";

    /// <summary>Terms and the signature block. Repeats on every page.</summary>
    private static string FixedFooter() =>
        $"<div>{Placeholder("Document.Terms")}</div>"
        + "<table width=\"100%\"><tbody><tr>"
        + "<td align=\"left\">&nbsp;</td>"
        + $"<td align=\"right\">For {Placeholder("Organization.Name")}</td>"
        + "</tr></tbody></table>";
}
