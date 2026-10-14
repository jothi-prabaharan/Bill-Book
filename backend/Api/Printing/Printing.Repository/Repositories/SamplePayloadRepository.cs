using Shared.Kernel.Printing;

namespace Printing.Repository.Repositories;

/// <summary>
/// Plausible data for previewing a template with no document behind it.
///
/// Built from the placeholder catalogue rather than written out per document
/// type, so a tag added to the catalogue is previewable the same day and cannot
/// be forgotten here. Values are typed the way a real payload types them —
/// decimals as decimals, dates as dates — so the preview exercises the same
/// formatting path a printed document does.
/// </summary>
public static class SamplePayloadRepository
{
    private const int SampleRowCount = 3;

    public static PrintPayload For(string documentTypeCode)
    {
        var payload = new PrintPayload();
        string docType = documentTypeCode?.Trim().ToUpperInvariant() ?? "INV";
        IReadOnlyList<PlaceholderDefinition> placeholders = PlaceholderCatalog.For(docType);

        foreach (PlaceholderDefinition placeholder in placeholders.Where(p => p.Kind == PlaceholderKind.Single))
        {
            payload.Singles[placeholder.Tag] = Value(placeholder, 1, docType);
        }

        foreach (IGrouping<string, PlaceholderDefinition> group in placeholders
            .Where(p => p.Kind == PlaceholderKind.List)
            .GroupBy(p => p.Group))
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            for (int row = 1; row <= SampleRowCount; row++)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PlaceholderDefinition placeholder in group)
                {
                    values[placeholder.Tag] = Value(placeholder, row, docType);
                }

                rows.Add(values);
            }

            payload.Lists[group.Key] = rows;
        }

        return payload;
    }

    private static string DocNo(string docType) => docType switch
    {
        "INV" => "SAMPLE-0001",
        "QTE" => "QT-2026-00319",
        "SOR" => "SO-2026-00943",
        "DLC" => "DC-2026-00731",
        "CRN" => "CN-2026-00042",
        "POR" => "PO-2026-00219",
        "GRN" => "GRN-2026-00589",
        "BIL" => "BIL-2026-01207",
        "DBN" => "DN-2026-00098",
        "RCM" => "REC-2026-00812",
        "SPM" => "PAY-2026-00477",
        "JRN" => "JV-2026-00319",
        "KOT" => "KOT-084",
        _ => "SAMPLE-0001",
    };

    private static object? Value(PlaceholderDefinition placeholder, int row, string docType) => placeholder.Type switch
    {
        PlaceholderType.Amount => AmountValue(placeholder.Tag, row),
        PlaceholderType.Number => NumberValue(placeholder.Tag, row, docType),
        PlaceholderType.Date => DateValue(placeholder.Tag, row),

        PlaceholderType.Image => placeholder.Tag == "Organization.Logo" ? "/assets/logo.png" : null,
        PlaceholderType.RichText => RichTextValue(placeholder.Tag, placeholder.Description, docType),
        PlaceholderType.QrCode => QrCodeValue(placeholder.Tag, docType),
        PlaceholderType.Barcode => DocNo(docType),

        _ => Sample(placeholder, row, docType),
    };

    private static decimal AmountValue(string tag, int row) => tag switch
    {
        "Totals.SubTotal" => 62316.00m,
        "Totals.Discount" => 500.00m,
        "Totals.TaxableValue" => 61816.00m,
        "Totals.Tax" => 11126.88m,
        "Totals.RoundOff" => 0.12m,
        "Totals.GrandTotal" => 72943.00m,
        "Totals.Paid" => 50000.00m,
        "Totals.Outstanding" => 22943.00m,

        "Item.Rate" => row switch { 1 => 78.50m, 2 => 1180.00m, _ => 512.00m },
        "Item.Discount" => row switch { 2 => 500.00m, _ => 0.00m },
        "Item.TaxableValue" => row switch { 1 => 9420.00m, 2 => 27820.00m, _ => 24576.00m },
        "Item.Amount" => row switch { 1 => 11115.60m, 2 => 32827.60m, _ => 28999.68m },

        "Tax.TaxableValue" => 61816.00m,
        "Tax.Amount" => row switch { 1 => 5563.44m, 2 => 5563.44m, _ => 11126.88m },

        "Alloc.DocumentTotal" => row switch { 1 => 84200.00m, 2 => 112400.00m, _ => 72943.00m },
        "Alloc.Amount" => row switch { 1 => 50000.00m, 2 => 22943.00m, _ => 72943.00m },
        "Alloc.Balance" => row switch { 1 => 34200.00m, 2 => 89457.00m, _ => 0.00m },

        "Line.Debit" => row switch { 1 => 18400.00m, 3 => 65000.00m, _ => 0.00m },
        "Line.Credit" => row switch { 2 => 18400.00m, _ => 0.00m },

        "Payment.Amount" => row switch { 1 => 35000.00m, _ => 15000.00m },

        _ => 1234.50m * row,
    };

    private static decimal NumberValue(string tag, int row, string docType) => tag switch
    {
        "Item.SlNo" => row,
        "Item.Quantity" => docType == "KOT"
            ? (row switch { 1 => 2m, 2 => 4m, _ => 1m })
            : (row switch { 1 => 120m, 2 => 24m, _ => 48m }),
        "Item.TaxRate" => 18.00m,
        "Tax.Rate" => row switch { 1 => 9.00m, 2 => 9.00m, _ => 18.00m },
        "Kitchen.Guests" => 4m,
        "Document.ExchangeRate" => 1.00m,
        _ => 2m * row,
    };

    private static DateOnly DateValue(string tag, int row) => tag switch
    {
        "Document.Date" => new DateOnly(2026, 9, 6),
        "Document.DueDate" => new DateOnly(2026, 9, 21),
        "Document.DeliveryDate" => new DateOnly(2026, 9, 15),
        "Document.EwayBillDate" => new DateOnly(2026, 9, 6),
        "Alloc.DocumentDate" => row switch { 1 => new DateOnly(2026, 8, 12), 2 => new DateOnly(2026, 8, 24), _ => new DateOnly(2026, 9, 6) },
        "Payment.Date" => row switch { 1 => new DateOnly(2026, 8, 28), _ => new DateOnly(2026, 9, 4) },
        "EInvoice.AckDate" => new DateOnly(2026, 9, 6),
        "EInvoice.EwbDate" => new DateOnly(2026, 9, 6),
        "EInvoice.EwbValidUntil" => new DateOnly(2026, 9, 9),
        _ => new DateOnly(2026, 9, 4).AddDays(row - 1),
    };

    private static string RichTextValue(string tag, string fallbackDescription, string docType) => tag switch
    {
        "Organization.Address" => "14 Fort Road, Ballard Estate, Fort<br>Mumbai, Maharashtra 400001",
        "Party.Address" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "Plot 42, MIDC Phase II, Chakan Industrial Area<br>Pune, Maharashtra 411026"
            : "3rd Floor, Prestige Atrium, Residency Road<br>Bengaluru, Karnataka 560025",
        "Party.ShippingAddress" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "14 Fort Road, Ballard Estate<br>Mumbai, Maharashtra 400001"
            : "Site 42, Outer Ring Road, Marathahalli<br>Bengaluru, Karnataka 560037",
        "Document.Notes" => "Thank you for your business. Please remit to our bank account or via UPI.",
        "Document.Terms" => "1. Goods once sold will not be taken back without prior authorization.<br>2. Interest @ 18% p.a. charged on overdue invoices.<br>3. Subject to Mumbai jurisdiction.",
        _ => $"<div>{fallbackDescription}</div>",
    };

    private static string QrCodeValue(string tag, string docType) => tag switch
    {
        PlaceholderCatalog.UpiQrTag => $"upi://pay?pa=sampletraders@hdfcbank&pn=Sample%20Traders&am=22943.00&cu=INR&tn={DocNo(docType)}",
        "EInvoice.QrImage" => "4a28f7d983e201b17d84a7e8f50c0012e8b23a9b1c7849e7a83d2948e7182903",
        _ => DocNo(docType),
    };

    private static string Sample(PlaceholderDefinition placeholder, int row, string docType) => placeholder.Tag switch
    {
        "Organization.Name" => "Sample Traders",
        "Organization.LegalName" => "Sample Traders Private Limited",
        "Organization.Gstin" => "27AABCU9603R1ZM",
        "Organization.StateName" => "Maharashtra",
        "Organization.Phone" => "+91 22 4021 8890",
        "Organization.Email" => "billing@sampletraders.example.com",
        "Organization.BankName" => "HDFC Bank Ltd",
        "Organization.BankAccountNo" => "50200012345678",
        "Organization.BankIfsc" => "HDFC0000042",
        "Organization.BankBranch" => "Fort Branch, Mumbai",
        "Organization.UpiId" => "sampletraders@hdfcbank",

        "Party.Name" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "Sundar Steel Works Pvt Ltd"
            : "Halcyon Interiors Pvt Ltd",
        "Party.Gstin" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "27AAFCS4471M1Z2"
            : "29AACCH8821P1ZB",
        "Party.StateName" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "Maharashtra"
            : "Karnataka",
        "Party.Phone" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "+91 20 2712 4400"
            : "+91 80 4123 9900",
        "Party.Email" => docType is "POR" or "BIL" or "GRN" or "DBN" or "SPM"
            ? "sales@sundarsteel.example.com"
            : "accounts@halcyoninteriors.com",

        "Document.No" => DocNo(docType),
        "Document.Reference" => docType switch
        {
            "INV" => "PO-2026-00219",
            "BIL" => "SSW/26/894",
            "CRN" => "SAMPLE-0001",
            "DBN" => "BIL-2026-01207",
            "DLC" => "SO-2026-00943",
            "GRN" => "PO-2026-00219",
            "RCM" => "UTR-402188173",
            "SPM" => "CMS-990142881",
            _ => "REF-2026-0042",
        },
        "Document.OriginalDocumentNo" => docType switch
        {
            "CRN" => "SAMPLE-0001",
            "DBN" => "BIL-2026-01207",
            "INV" => "SO-2026-00943",
            _ => "SAMPLE-0001",
        },
        "Document.Reason" => docType switch
        {
            "CRN" => "Sales Return — Material specification mismatch",
            "DBN" => "Goods rejected on quality inspection",
            _ => "Adjustment against return",
        },
        "Document.PlaceOfSupply" => "29 - Karnataka",
        "Document.Currency" => "INR",
        "Document.Status" => "Posted",
        "Document.VehicleNo" => "MH 12 QT 4417",
        "Document.TransporterName" => "VRL Logistics Ltd",
        "Document.EwayBillNo" => "241088491028",
        "Document.PaymentMode" => "NEFT / Bank Transfer",
        "Document.BankAccountName" => "HDFC Current Account",

        "Totals.AmountInWords" => "Rupees Seventy Two Thousand Nine Hundred Forty Three only",

        "Item.ItemName" => docType == "KOT"
            ? (row switch { 1 => "Paneer Butter Masala", 2 => "Butter Naan", _ => "Jeera Rice" })
            : (row switch { 1 => "Mild steel angle 50×50×6 mm", 2 => "Galvanised sheet 1.6 mm", _ => "SS 304 seamless pipe 25 mm OD" }),
        "Item.Description" => docType == "KOT"
            ? (row switch { 1 => "Medium spicy, extra butter", 2 => "Crisp, well done", _ => "Regular portion" })
            : (row switch { 1 => "Grade IS 2062 E250A — standard lengths 6m", 2 => "Class 8 zero spangle zinc coated 120 gsm", _ => "Schedule 10 thickness annealed & pickled" }),
        "Item.HsnSac" => row switch { 1 => "7216", 2 => "7210", _ => "7306" },
        "Item.Uom" => docType == "KOT"
            ? (row switch { 1 => "portion", 2 => "pcs", _ => "bowl" })
            : (row switch { 1 => "kg", 2 => "sheet", _ => "m" }),

        "Tax.Component" => row switch { 1 => "CGST", 2 => "SGST", _ => "IGST" },

        "Alloc.DocumentNo" => row switch { 1 => "INV/2026/00311", 2 => "INV/2026/00344", _ => "INV/2026/00418" },

        "Line.AccountCode" => row switch { 1 => "4010", 2 => "1510", _ => "5020" },
        "Line.AccountName" => row switch { 1 => "Depreciation Expense", 2 => "Accumulated Depreciation", _ => "Office Rent Expense" },
        "Line.Description" => row switch { 1 => "Plant & machinery depreciation for Sep 2026", 2 => "Depreciation reserve provision", _ => "September 2026 premises rent" },

        "Payment.Mode" => row switch { 1 => "Bank transfer (NEFT)", _ => "UPI transfer" },
        "Payment.Reference" => row switch { 1 => "UTR 402188173", _ => "UPI 6620041188" },

        "Kitchen.Station" => "Main Kitchen",
        "Kitchen.Table" => "Table 14",
        "Kitchen.OrderNo" => "ORD-2041",
        "Kitchen.OrderType" => "Dine-in",
        "Kitchen.Waiter" => "Arun Kumar",
        "Kitchen.Time" => "08:42 PM",
        "Kitchen.Copy" => "ORIGINAL",

        "EInvoice.Irn" => "4a28f7d983e201b17d84a7e8f50c0012e8b23a9b1c7849e7a83d2948e7182903",
        "EInvoice.AckNo" => "1126100482910",
        "EInvoice.EwbNo" => "241088491028",

        _ => $"{placeholder.Description} {row}",
    };
}
