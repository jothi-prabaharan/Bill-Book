using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Printing;

/// <summary>One line of a trading document and the tax rows saved with it, whatever the document is.</summary>
public sealed record DocumentPrintLine(
    DocumentLineBase Line,
    IReadOnlyList<DocumentLineTaxBase> Taxes,
    string? DescriptionSuffix = null);

/// <summary>
/// A sales or purchase document as the print template reads it, keyed by the tags
/// <see cref="PlaceholderCatalog"/> names. The nine trading documents share one header and one line shape
/// (<see cref="DocumentHeaderBase"/>, <see cref="DocumentLineBase"/>), so one builder serves all of them; each
/// service's print repository adds the few tags only its document has, and sets
/// <c>Document.PlaceOfSupply</c>, which Sales and Purchase resolve differently.
///
/// <b>Values, not formatted text</b>: amounts go out as decimals and dates as dates, and the template's masks
/// format them in Printing. Formatting here would be a service deciding presentation, which is what templates
/// take away from it. Pure, so what a document prints as can be tested without a service or a database.
///
/// The organization is the issuer on a sale and the buyer on a purchase, and the party is the other side, so
/// the same tags carry either. <c>Totals.AmountInWords</c>, <c>Item.Uom</c>, <c>Party.StateName</c>,
/// <c>Totals.Paid</c> and <c>Totals.Outstanding</c> are not filled: no trading service holds them, and they
/// print as nothing rather than as a guess.
/// </summary>
public static class DocumentPrintPayload
{
    public static PrintPayload Build(
        DocumentHeaderBase document,
        IReadOnlyList<DocumentPrintLine> lines,
        OrgIdentity organization,
        string partyName,
        IReadOnlyDictionary<long, string> itemNames)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(itemNames);

        var payload = new PrintPayload();
        var singles = payload.Singles;

        AddOrganization(payload, organization);

        singles["Document.No"] = document.DocumentNo;
        singles["Document.Date"] = document.DocumentDate;
        singles["Document.Status"] = document.Status.ToString();
        singles["Document.Currency"] = document.CurrencyCode;
        singles["Document.ExchangeRate"] = document.ExchangeRate;
        singles["Document.Notes"] = PrintText.Lines(document.Notes);
        singles["Document.Terms"] = PrintText.Lines(document.TermsAndConditions);

        singles["Party.Name"] = partyName;
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

        List<DocumentPrintLine> ordered = [.. lines.OrderBy(l => l.Line.LineNumber)];

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

        // Per component and per rate: a single "CGST" row is wrong the moment a document mixes slabs, as a
        // jeweller's bill of 3% bullion beside 18% making charges does.
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
    /// The branch's letterhead tags, the same on every document it issues: its name, GSTIN, state, contact
    /// details and address. A service whose document is not a <see cref="DocumentHeaderBase"/> (a receipt, a
    /// journal) calls this for the part of the payload every type shares.
    /// </summary>
    public static void AddOrganization(PrintPayload payload, OrgIdentity organization)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(organization);

        var singles = payload.Singles;

        singles["Organization.Name"] = organization.Name;
        singles["Organization.LegalName"] = organization.Name;
        singles["Organization.Gstin"] = organization.Gstin;
        singles["Organization.StateName"] = organization.StateCode;
        singles["Organization.Phone"] = organization.PhoneNumber;
        singles["Organization.Email"] = organization.Email;
        singles["Organization.Address"] = PrintText.Lines(
            organization.AddressLine1,
            organization.AddressLine2,
            PrintText.JoinNonEmpty(" ", organization.City, organization.PostalCode));
    }

    /// <summary>
    /// What a document is stamped with. A posted one prints clean; a voided one says so; any other is not
    /// final and is stamped DRAFT, so a draft order is never mistaken for one the other party was sent.
    /// </summary>
    public static string? Watermark(DocumentStatus status) => status switch
    {
        DocumentStatus.Posted => null,
        DocumentStatus.Void => "VOID",
        _ => "DRAFT",
    };
}
