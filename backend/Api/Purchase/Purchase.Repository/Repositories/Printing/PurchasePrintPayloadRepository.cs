using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository.Repositories;

/// <summary>One line of a purchase document and the tax rows saved with it, whatever the document is.</summary>
public sealed record PurchasePrintLine(
    DocumentLineBase Line,
    IReadOnlyList<DocumentLineTaxBase> Taxes,
    string? DescriptionSuffix = null);

/// <summary>
/// A purchase document as the print template reads it, keyed by the tags <see cref="PlaceholderCatalog"/>
/// names. The purchase order, goods receipt, bill and debit note share one header and one line shape
/// (<see cref="DocumentHeaderBase"/>, <see cref="DocumentLineBase"/>), so one builder serves all four; each
/// print repository adds the few tags only its document has.
///
/// <b>Values, not formatted text</b>: amounts go out as decimals and dates as dates, and the template's masks
/// format them in Printing, as the sales invoice does. Pure, so what a document prints as can be tested
/// without a service or a database.
///
/// The party is the <b>vendor</b> and the organization is the buyer, the other way round from a sale.
/// <c>Totals.AmountInWords</c>, <c>Item.Uom</c> and <c>Party.StateName</c> are not filled, as on the invoice:
/// this service holds none of them, and they print as nothing rather than as a guess.
/// </summary>
public static class PurchasePrintPayloadRepository
{
    public static PrintPayload Build(
        DocumentHeaderBase document,
        IReadOnlyList<PurchasePrintLine> lines,
        OrgIdentity buyer,
        string vendorName,
        IReadOnlyDictionary<long, string> itemNames)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(itemNames);

        // The header, party, item and tax payload is the one every trading document shares
        // (Shared.Kernel.Printing); a purchase adds where the supply is made to the buyer.
        PrintPayload payload = DocumentPrintPayload.Build(
            document,
            [.. lines.Select(l => new DocumentPrintLine(l.Line, l.Taxes, l.DescriptionSuffix))],
            buyer,
            vendorName,
            itemNames);

        payload.Singles["Document.PlaceOfSupply"] = PlaceOfSupply(document.PlaceOfSupplyStateId, buyer.StateCode);

        return payload;
    }

    /// <summary>
    /// The state the supply is made in, as its two-digit GST code. A purchase is supplied to the buyer, so
    /// it is the document's own state when one was recorded, else the branch's.
    /// </summary>
    public static string? PlaceOfSupply(int placeOfSupplyStateId, string? branchStateCode)
    {
        if (placeOfSupplyStateId is > 0 and < 100)
        {
            return placeOfSupplyStateId.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.IsNullOrWhiteSpace(branchStateCode) ? null : branchStateCode.Trim();
    }

    /// <summary>
    /// What a purchase document is stamped with. A posted one prints clean; a voided one says so; any other
    /// is not final and is stamped DRAFT, so a draft order is never mistaken for one the vendor was sent.
    /// </summary>
    public static string? Watermark(DocumentStatus status) => DocumentPrintPayload.Watermark(status);
}
