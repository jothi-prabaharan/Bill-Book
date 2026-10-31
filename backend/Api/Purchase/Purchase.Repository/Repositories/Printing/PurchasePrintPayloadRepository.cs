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

        var payload = new PrintPayload();
        var singles = payload.Singles;

        singles["Organization.Name"] = buyer.Name;
        singles["Organization.LegalName"] = buyer.Name;
        singles["Organization.Gstin"] = buyer.Gstin;
        singles["Organization.StateName"] = buyer.StateCode;
        singles["Organization.Phone"] = buyer.PhoneNumber;
        singles["Organization.Email"] = buyer.Email;
        singles["Organization.Address"] = PrintText.Lines(
            buyer.AddressLine1,
            buyer.AddressLine2,
            PrintText.JoinNonEmpty(" ", buyer.City, buyer.PostalCode));

        singles["Document.No"] = document.DocumentNo;
        singles["Document.Date"] = document.DocumentDate;
        singles["Document.Status"] = document.Status.ToString();
        singles["Document.PlaceOfSupply"] = PlaceOfSupply(document.PlaceOfSupplyStateId, buyer.StateCode);
        singles["Document.Currency"] = document.CurrencyCode;
        singles["Document.ExchangeRate"] = document.ExchangeRate;
        singles["Document.Notes"] = PrintText.Lines(document.Notes);
        singles["Document.Terms"] = PrintText.Lines(document.TermsAndConditions);

        singles["Party.Name"] = vendorName;
        singles["Party.Gstin"] = document.ContactGstin;
        singles["Party.Address"] = PrintText.Lines(document.BillingAddress);
        singles["Party.ShippingAddress"] = PrintText.Lines(document.ShippingAddress);

        decimal tax = document.CgstAmount + document.SgstAmount + document.IgstAmount + document.CessAmount;

        singles["Totals.SubTotal"] = document.SubTotal;
        singles["Totals.Discount"] = document.DiscountAmount;
        singles["Totals.TaxableValue"] = document.TaxableAmount;
        singles["Totals.Tax"] = tax;
        singles["Totals.RoundOff"] = document.RoundOffAmount;
        singles["Totals.GrandTotal"] = document.TotalAmount;

        List<PurchasePrintLine> ordered = [.. lines.OrderBy(l => l.Line.LineNumber)];

        payload.Lists["Item"] = [.. ordered.Select(entry => (IReadOnlyDictionary<string, object?>)
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item.SlNo"] = entry.Line.LineNumber,
                ["Item.ItemName"] = entry.Line.ItemId is long itemId && itemNames.TryGetValue(itemId, out string? name)
                    ? name
                    : entry.Line.Description,
                ["Item.Description"] = PrintText.JoinNonEmpty(" — ", entry.Line.Description, entry.DescriptionSuffix),
                ["Item.HsnSac"] = entry.Line.HsnSacCode,
                ["Item.Quantity"] = entry.Line.Quantity,
                ["Item.Rate"] = entry.Line.UnitPrice,
                ["Item.Discount"] = entry.Line.DiscountAmount,
                ["Item.TaxableValue"] = entry.Line.TaxableAmount,
                ["Item.TaxRate"] = entry.Taxes.Sum(t => t.Rate),
                ["Item.Amount"] = entry.Line.LineTotal,
            })];

        // Per component and per rate, as the invoice does: a single "CGST" row is wrong the moment a
        // document mixes slabs.
        payload.Lists["Tax"] = [.. ordered
            .SelectMany(entry => entry.Taxes)
            .GroupBy(t => (t.TaxComponent, t.Rate))
            .OrderBy(g => g.Key.TaxComponent)
            .ThenBy(g => g.Key.Rate)
            .Select(g => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tax.Component"] = PrintText.ComponentName(g.Key.TaxComponent),
                    ["Tax.Rate"] = g.Key.Rate,
                    ["Tax.TaxableValue"] = g.Sum(t => t.TaxableAmount),
                    ["Tax.Amount"] = g.Sum(t => t.Amount),
                })];

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
    public static string? Watermark(DocumentStatus status) => status switch
    {
        DocumentStatus.Posted => null,
        DocumentStatus.Void => "VOID",
        _ => "DRAFT",
    };
}
