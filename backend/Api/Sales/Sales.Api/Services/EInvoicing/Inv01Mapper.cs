using System.Globalization;
using Sales.Entity.Enums;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>One tax row of a document line, as the document stored it.</summary>
public sealed record EInvoiceInputTax(TaxComponent Component, decimal Rate, decimal Amount);

/// <summary>A document line, in the figures the document already holds (TK-91).</summary>
public sealed record EInvoiceInputLine(
    int LineNumber,
    string? Description,
    string? HsnSacCode,
    decimal Quantity,
    string? UqcCode,

    /// <summary>The discount that reduced the taxable value; zero for a settlement discount, which did not.</summary>
    decimal DiscountAmount,
    decimal TaxableAmount,
    IReadOnlyList<EInvoiceInputTax> Taxes);

/// <summary>
/// Everything the INV-01 document is built from, gathered by
/// <see cref="EInvoiceDocumentBuilder"/> from the document, Master and Inventory.
/// The mapper is pure over this, so it is tested without any of them.
/// </summary>
public sealed record EInvoiceInput(
    EInvoiceSource Kind,
    string DocumentNo,
    DateOnly DocumentDate,
    OrgIdentity Seller,
    ContactPostalAddress Buyer,

    /// <summary>The buyer's GST registration type by name: Regular, Composition, Sez, Overseas, and so on.</summary>
    string? BuyerRegistrationType,
    bool IsInterState,
    IReadOnlyList<EInvoiceInputLine> Lines,
    decimal TotalAmount,
    decimal RoundOffAmount,

    /// <summary>For a credit note, the invoice it corrects.</summary>
    string? PrecedingDocumentNo = null,
    DateOnly? PrecedingDocumentDate = null);

/// <summary>
/// Turns a sales invoice or credit note into the notified INV-01 document
/// (TK-91). Pure: no I/O, no clock.
///
/// <b>The document's own figures are sent, never recomputed.</b> The invoice is
/// a statement of amounts that were communicated; the IRP registers what the
/// buyer was told. Each line's assessable value is its taxable amount, its
/// discount the part that reduced it, and the total the document's total. A
/// settlement discount, which does not reduce the taxable value, comes out as
/// the difference between the lines and the total and goes in the value block's
/// discount, which is where the schema puts it.
/// </summary>
public static class Inv01Mapper
{
    /// <summary>The state code and PIN the schema asks for on a buyer outside India.</summary>
    public const string ExportStateCode = "96";

    public const int ExportPin = 999999;

    /// <summary>The GSTIN the schema asks for on an unregistered (overseas) buyer.</summary>
    public const string UnregisteredBuyer = "URP";

    public static Inv01Document Map(EInvoiceInput input)
    {
        decimal igstTotal = input.Lines.Sum(l => Amount(l, TaxComponent.Igst));
        string supplyType = EInvoiceApplicability.SupplyTypeFor(
            input.BuyerRegistrationType, input.Buyer.Gstin, igstTotal > 0) ?? "B2B";
        bool export = supplyType.StartsWith("EXP", StringComparison.Ordinal);

        List<Inv01Item> items = [.. input.Lines.OrderBy(l => l.LineNumber).Select((l, i) => Item(l, i + 1))];

        decimal assVal = items.Sum(i => i.AssAmt);
        decimal cgst = items.Sum(i => i.CgstAmt);
        decimal sgst = items.Sum(i => i.SgstAmt);
        decimal igst = items.Sum(i => i.IgstAmt);
        decimal cess = items.Sum(i => i.CesAmt);
        decimal belowTheLines = Round2(assVal + cgst + sgst + igst + cess + input.RoundOffAmount - input.TotalAmount);

        return new Inv01Document
        {
            TranDtls = new Inv01Transaction { SupTyp = supplyType },
            DocDtls = new Inv01DocumentDetails
            {
                Typ = DocumentType(input.Kind),
                No = input.DocumentNo,
                Dt = Date(input.DocumentDate),
            },
            SellerDtls = new Inv01Party
            {
                Gstin = input.Seller.Gstin ?? string.Empty,
                LglNm = input.Seller.Name,
                Addr1 = input.Seller.AddressLine1 ?? string.Empty,
                Addr2 = Blank(input.Seller.AddressLine2),
                Loc = input.Seller.City ?? string.Empty,
                Pin = Pin(input.Seller.PostalCode),
                Stcd = input.Seller.StateCode ?? string.Empty,
                Ph = Phone(input.Seller.PhoneNumber),
                Em = Blank(input.Seller.Email),
            },
            BuyerDtls = new Inv01Buyer
            {
                Gstin = export ? UnregisteredBuyer : input.Buyer.Gstin ?? string.Empty,
                LglNm = input.Buyer.LegalName,
                Pos = export ? ExportStateCode : PlaceOfSupply(input),
                Addr1 = input.Buyer.AddressLine1 ?? string.Empty,
                Addr2 = Blank(input.Buyer.AddressLine2),
                Loc = input.Buyer.City ?? string.Empty,
                Pin = export ? ExportPin : Pin(input.Buyer.PostalCode),
                Stcd = export ? ExportStateCode : input.Buyer.StateCode ?? Shared.Kernel.Tax.Gstin.StateCodeOf(input.Buyer.Gstin) ?? string.Empty,
                Ph = Phone(input.Buyer.PhoneNumber),
            },
            ItemList = items,
            ValDtls = new Inv01Values
            {
                AssVal = assVal,
                CgstVal = cgst,
                SgstVal = sgst,
                IgstVal = igst,
                CesVal = cess,
                Discount = belowTheLines > 0 ? belowTheLines : 0,
                RndOffAmt = input.RoundOffAmount,
                TotInvVal = input.TotalAmount,
            },
            RefDtls = input.PrecedingDocumentNo is { Length: > 0 } no && input.PrecedingDocumentDate is DateOnly on
                ? new Inv01References { PrecDocDtls = [new Inv01PrecedingDocument { InvNo = no, InvDt = Date(on) }] }
                : null,
        };
    }

    /// <summary><c>dd/MM/yyyy</c>, the schema's date format.</summary>
    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DocumentType(EInvoiceSource kind) => kind switch
    {
        EInvoiceSource.CreditNote => "CRN",
        EInvoiceSource.DebitNote => "DBN",
        _ => "INV",
    };

    private static Inv01Item Item(EInvoiceInputLine line, int serial)
    {
        decimal assessable = Round2(line.TaxableAmount);
        decimal discount = Round2(Math.Max(0m, line.DiscountAmount));
        decimal gross = assessable + discount;
        decimal cgst = Amount(line, TaxComponent.Cgst);
        decimal sgst = Amount(line, TaxComponent.Sgst) + Amount(line, TaxComponent.Utgst);
        decimal igst = Amount(line, TaxComponent.Igst);
        decimal cess = Amount(line, TaxComponent.Cess);
        string hsn = (line.HsnSacCode ?? string.Empty).Trim();

        return new Inv01Item
        {
            SlNo = serial.ToString(CultureInfo.InvariantCulture),
            PrdDesc = Blank(line.Description),
            IsServc = hsn.StartsWith("99", StringComparison.Ordinal) ? "Y" : "N",
            HsnCd = hsn,
            Qty = Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero),
            Unit = Blank(line.UqcCode),
            UnitPrice = line.Quantity == 0
                ? gross
                : Math.Round(gross / line.Quantity, 3, MidpointRounding.AwayFromZero),
            TotAmt = gross,
            Discount = discount,
            AssAmt = assessable,
            GstRt = igst > 0 || Rate(line, TaxComponent.Igst) > 0
                ? Rate(line, TaxComponent.Igst)
                : Rate(line, TaxComponent.Cgst) + Rate(line, TaxComponent.Sgst) + Rate(line, TaxComponent.Utgst),
            CgstAmt = cgst,
            SgstAmt = sgst,
            IgstAmt = igst,
            CesRt = Rate(line, TaxComponent.Cess),
            CesAmt = cess,
            TotItemVal = assessable + cgst + sgst + igst + cess,
        };
    }

    /// <summary>
    /// The place of supply. The document keeps whether it was inter-state, not
    /// the state itself, so an intra-state supply is the seller's state, and an
    /// inter-state one the buyer's registration, then the buyer's address.
    /// </summary>
    private static string PlaceOfSupply(EInvoiceInput input) =>
        !input.IsInterState
            ? input.Seller.StateCode ?? string.Empty
            : Shared.Kernel.Tax.Gstin.StateCodeOf(input.Buyer.Gstin) ?? input.Buyer.StateCode ?? string.Empty;

    private static decimal Amount(EInvoiceInputLine line, TaxComponent component) =>
        Round2(line.Taxes.Where(t => t.Component == component).Sum(t => t.Amount));

    private static decimal Rate(EInvoiceInputLine line, TaxComponent component) =>
        line.Taxes.Where(t => t.Component == component).Select(t => t.Rate).FirstOrDefault();

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>A PIN as the number the schema asks for; zero when it is not six digits, which validation names.</summary>
    private static int Pin(string? postalCode) =>
        postalCode?.Trim() is { Length: 6 } pin && int.TryParse(pin, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;

    /// <summary>Digits only, as the schema takes a phone (6 to 12 of them); anything else is left out.</summary>
    private static string? Phone(string? phone)
    {
        string digits = new((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is >= 6 and <= 12 ? digits : null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Which supplies need an IRN (TK-91): B2B, exports and SEZ, and their notes. Never B2C.</summary>
public static class EInvoiceApplicability
{
    /// <summary>
    /// The INV-01 supply type for a buyer, or null for a supply that needs no
    /// IRN (a consumer, or an unregistered buyer in India).
    /// </summary>
    public static string? SupplyTypeFor(string? buyerRegistrationType, string? buyerGstin, bool igstCharged) =>
        buyerRegistrationType switch
        {
            "Overseas" => igstCharged ? "EXPWP" : "EXPWOP",
            "Sez" => igstCharged ? "SEZWP" : "SEZWOP",
            "Consumer" => null,
            _ => string.IsNullOrWhiteSpace(buyerGstin) ? null : "B2B",
        };

    /// <summary>
    /// Whether a document dated <paramref name="documentDate"/> needs an IRN on a
    /// branch that e-invoices from <paramref name="eInvoiceFrom"/>.
    /// </summary>
    public static bool BranchApplies(DateOnly? eInvoiceFrom, DateOnly documentDate) =>
        eInvoiceFrom is DateOnly from && documentDate >= from;
}
