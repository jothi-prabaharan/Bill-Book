using System.Net;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.Printing;

/// <summary>
/// An invoice as the print template reads it: every value keyed by the tag
/// <see cref="PlaceholderCatalog"/> names, and left as its own type.
///
/// <b>Values, not formatted text.</b> Amounts go out as decimals and dates as
/// dates, and the template's masks format them in Printing. Formatting here
/// would be Sales deciding presentation, which is what templates take away
/// from it.
///
/// Pure, so what an invoice prints as can be tested without a service or a
/// database.
///
/// <b>Not filled, and why</b> — each prints as nothing rather than as a guess:
/// <list type="bullet">
/// <item><c>Document.PlaceOfSupply</c> carries the two-digit state code, not the
/// state's name: Sales stores <c>PlaceOfSupplyStateId = 0</c> on every
/// document, so the code is taken from the customer's GSTIN, or the branch's
/// own for a counter sale.</item>
/// <item><c>Totals.AmountInWords</c>: there is no C# spelling of an amount yet,
/// only the TypeScript one in currency-format.</item>
/// <item><c>Item.Uom</c>, <c>Party.StateName</c>, <c>Totals.Paid</c> and
/// <c>Totals.Outstanding</c>: Sales holds none of them.</item>
/// </list>
/// </summary>
public static class InvoicePrintPayload
{
    public static PrintPayload Build(
        Invoice invoice,
        OrgIdentity seller,
        string customerName,
        IReadOnlyDictionary<long, string> itemNames)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(itemNames);

        var payload = new PrintPayload();
        var singles = payload.Singles;

        singles["Organization.Name"] = seller.Name;
        singles["Organization.Gstin"] = seller.Gstin;
        singles["Organization.Address"] = Lines(
            seller.AddressLine1,
            seller.AddressLine2,
            JoinNonEmpty(" ", seller.City, seller.PostalCode));

        singles["Document.No"] = invoice.DocumentNo;
        singles["Document.Date"] = invoice.DocumentDate;
        singles["Document.DueDate"] = invoice.DueDate;
        singles["Document.Status"] = invoice.Status.ToString();
        singles["Document.PlaceOfSupply"] = PlaceOfSupply(invoice.ContactGstin, seller.StateCode);
        singles["Document.Currency"] = invoice.CurrencyCode;
        singles["Document.Notes"] = Lines(invoice.Notes);
        singles["Document.Terms"] = Lines(invoice.TermsAndConditions);

        singles["Party.Name"] = customerName;
        singles["Party.Gstin"] = invoice.ContactGstin;
        singles["Party.Address"] = Lines(invoice.BillingAddress);
        singles["Party.ShippingAddress"] = Lines(invoice.ShippingAddress);

        decimal tax = invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount + invoice.CessAmount;

        singles["Totals.SubTotal"] = invoice.SubTotal;
        singles["Totals.Discount"] = invoice.DiscountAmount;
        singles["Totals.TaxableValue"] = invoice.TaxableAmount;
        singles["Totals.Tax"] = tax;
        singles["Totals.RoundOff"] = invoice.RoundOffAmount;
        singles["Totals.GrandTotal"] = invoice.TotalAmount;

        List<InvoiceDetail> lines = [.. invoice.Lines.OrderBy(l => l.LineNumber)];

        payload.Lists["Item"] = [.. lines.Select(line => (IReadOnlyDictionary<string, object?>)
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item.SlNo"] = line.LineNumber,
                ["Item.ItemName"] = line.ItemId is long itemId && itemNames.TryGetValue(itemId, out string? name)
                    ? name
                    : line.Description,
                ["Item.Description"] = line.Description,
                ["Item.HsnSac"] = line.HsnSacCode,
                ["Item.Quantity"] = line.Quantity,
                ["Item.Rate"] = line.UnitPrice,
                ["Item.Discount"] = line.DiscountAmount,
                ["Item.TaxableValue"] = line.TaxableAmount,
                ["Item.TaxRate"] = line.Taxes.Sum(t => t.Rate),
                ["Item.Amount"] = line.LineTotal,
            })];

        // Per component and per rate, which is how a return reads it: a single
        // "CGST" row is wrong the moment an invoice mixes slabs, as a jeweller's
        // bill of 3% bullion beside 18% making charges does.
        payload.Lists["Tax"] = [.. lines
            .SelectMany(line => line.Taxes)
            .GroupBy(t => (t.TaxComponent, t.Rate))
            .OrderBy(g => g.Key.TaxComponent)
            .ThenBy(g => g.Key.Rate)
            .Select(g => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tax.Component"] = ComponentName(g.Key.TaxComponent),
                    ["Tax.Rate"] = g.Key.Rate,
                    ["Tax.TaxableValue"] = g.Sum(t => t.TaxableAmount),
                    ["Tax.Amount"] = g.Sum(t => t.Amount),
                })];

        return payload;
    }

    /// <summary>
    /// The state the supply is made in, as its two-digit GST code. A registered
    /// customer's GSTIN names their state; a customer with none is supplied at
    /// the counter, in the branch's own state.
    /// </summary>
    public static string? PlaceOfSupply(string? customerGstin, string? branchStateCode)
    {
        string gstin = customerGstin?.Trim() ?? string.Empty;
        if (gstin.Length >= 2 && char.IsAsciiDigit(gstin[0]) && char.IsAsciiDigit(gstin[1]))
        {
            return gstin[..2];
        }

        return string.IsNullOrWhiteSpace(branchStateCode) ? null : branchStateCode.Trim();
    }

    private static string ComponentName(TaxComponent component) => component switch
    {
        TaxComponent.Cgst => "CGST",
        TaxComponent.Sgst => "SGST",
        TaxComponent.Igst => "IGST",
        TaxComponent.Utgst => "UTGST",
        TaxComponent.Cess => "Cess",
        _ => component.ToString(),
    };

    /// <summary>
    /// Free text as rich-text markup: escaped, with its line breaks kept. The
    /// address and terms placeholders are rich text, which the renderer
    /// sanitises; text typed into a form is not markup, so it is escaped first
    /// rather than trusted to mean what its angle brackets say.
    /// </summary>
    private static string? Lines(params string?[] parts)
    {
        string[] present = [.. parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .SelectMany(p => p!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => WebUtility.HtmlEncode(p))];

        return present.Length == 0 ? null : string.Join("<br>", present);
    }

    private static string? JoinNonEmpty(string separator, params string?[] parts)
    {
        string joined = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return joined.Length == 0 ? null : joined;
    }
}
