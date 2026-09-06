using System.Text;

namespace Shared.Kernel.Printing;

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
    /// </summary>
    public const int SeedVersion = 1;

    /// <summary>A merge chip, exactly as the sanitiser's allow-list and the renderer both expect it.</summary>
    public static string Chip(PlaceholderDefinition placeholder) =>
        placeholder.Kind == PlaceholderKind.List
            ? $"<span contenteditable=\"false\" class=\"pt-chip\">↻«{placeholder.Tag}»</span>"
            : $"<span contenteditable=\"false\" class=\"pt-chip\">«{placeholder.Tag}»</span>";

    /// <summary>A chip by tag, for building markup without looking the definition up first.</summary>
    public static string Chip(string tag, PlaceholderKind kind = PlaceholderKind.Single) =>
        kind == PlaceholderKind.List
            ? $"<span contenteditable=\"false\" class=\"pt-chip\">↻«{tag}»</span>"
            : $"<span contenteditable=\"false\" class=\"pt-chip\">«{tag}»</span>";

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
        + $"<td width=\"20%\">{Chip("Organization.Logo")}</td>"
        + "<td align=\"left\">"
        + $"<div style=\"font-weight: bold\">{Chip("Organization.Name")}</div>"
        + $"<div>{Chip("Organization.Address")}</div>"
        + $"<div>GSTIN: {Chip("Organization.Gstin")}</div>"
        + "</td>"
        + "<td align=\"right\">"
        + $"<div>{Chip("Organization.Phone")}</div>"
        + $"<div>{Chip("Organization.Email")}</div>"
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
            html.Append($"<div style=\"font-weight: bold\">{Chip("Party.Name")}</div>");
            html.Append($"<div>{Chip("Party.Address")}</div>");
            html.Append($"<div>GSTIN: {Chip("Party.Gstin")}</div>");
            html.Append("</td>");
        }

        html.Append("<td align=\"right\">");
        html.Append($"<div>No: {Chip("Document.No")}</div>");
        html.Append($"<div>Date: {Chip("Document.Date")}</div>");

        if (profile.HasTax)
        {
            html.Append($"<div>Place of supply: {Chip("Document.PlaceOfSupply")}</div>");
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
                Chip("Item.SlNo", PlaceholderKind.List),
                Chip("Item.ItemName", PlaceholderKind.List),
                Chip("Item.HsnSac", PlaceholderKind.List),
                Chip("Item.Quantity", PlaceholderKind.List),
                Chip("Item.Rate", PlaceholderKind.List),
                Chip("Item.Amount", PlaceholderKind.List),
            ],
            ["left", "left", "left", "right", "right", "right"]),

        LineShape.Allocation => Rows(
            ["Document", "Date", "Document total", "Applied"],
            [
                Chip("Alloc.DocumentNo", PlaceholderKind.List),
                Chip("Alloc.DocumentDate", PlaceholderKind.List),
                Chip("Alloc.DocumentTotal", PlaceholderKind.List),
                Chip("Alloc.Amount", PlaceholderKind.List),
            ],
            ["left", "left", "right", "right"]),

        LineShape.Ledger => Rows(
            ["Account", "Narration", "Debit", "Credit"],
            [
                Chip("Line.AccountName", PlaceholderKind.List),
                Chip("Line.Description", PlaceholderKind.List),
                Chip("Line.Debit", PlaceholderKind.List),
                Chip("Line.Credit", PlaceholderKind.List),
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
                .Append($"<td align=\"left\">{Chip("Tax.Component", PlaceholderKind.List)}</td>")
                .Append($"<td align=\"right\">{Chip("Tax.Rate", PlaceholderKind.List)}</td>")
                .Append($"<td align=\"right\">{Chip("Tax.TaxableValue", PlaceholderKind.List)}</td>")
                .Append($"<td align=\"right\">{Chip("Tax.Amount", PlaceholderKind.List)}</td>")
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
                .Append($"<td align=\"right\" style=\"font-weight: bold\">{Chip("Totals.GrandTotal")}</td></tr>");
        }

        if (profile.HasPayments)
        {
            html.Append(TotalRow("Paid", "Totals.Paid"));
            html.Append(TotalRow("Outstanding", "Totals.Outstanding"));
        }

        html.Append("</tbody></table></td></tr></tbody></table>");

        if (profile.HasTotals)
        {
            html.Append($"<div>Amount in words: {Chip("Totals.AmountInWords")}</div>");
        }

        return html.ToString();
    }

    private static string TotalRow(string label, string tag) =>
        $"<tr><td align=\"left\">{label}</td><td align=\"right\">{Chip(tag)}</td></tr>";

    /// <summary>Terms and the signature block. Repeats on every page.</summary>
    private static string FixedFooter() =>
        $"<div>{Chip("Document.Terms")}</div>"
        + "<table width=\"100%\"><tbody><tr>"
        + "<td align=\"left\">&nbsp;</td>"
        + $"<td align=\"right\">For {Chip("Organization.Name")}</td>"
        + "</tr></tbody></table>";
}
