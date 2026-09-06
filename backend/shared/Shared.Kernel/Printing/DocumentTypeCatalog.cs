namespace Shared.Kernel.Printing;

/// <summary>Who the document is addressed to, which decides the party block.</summary>
public enum PartyKind
{
    None = 0,
    Customer = 1,
    Vendor = 2,
}

/// <summary>
/// The shape of the repeating block in the details segment. It is what makes
/// the generated layouts differ from one another — everything else about a
/// document is a heading.
/// </summary>
public enum LineShape
{
    /// <summary>Goods or services: quantity, rate, tax, amount.</summary>
    Item = 0,

    /// <summary>Money against documents: which invoice or bill, and how much of it.</summary>
    Allocation = 1,

    /// <summary>Accounts and two money columns. A journal voucher.</summary>
    Ledger = 2,
}

/// <summary>What one printable document type is made of.</summary>
public sealed class DocumentTypeProfile
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public PartyKind Party { get; init; }

    public LineShape Lines { get; init; }

    /// <summary>Carries a GST summary — the per-component, per-rate block.</summary>
    public bool HasTax { get; init; }

    public bool HasTotals { get; init; }

    /// <summary>Carries a payments or receipts block beneath the totals.</summary>
    public bool HasPayments { get; init; }
}

/// <summary>
/// The printable document types.
///
/// <b>Derived, and worth checking.</b> The specification names twelve; these
/// twelve are the printable subset of the seventeen codes already seeded in
/// mst.TransactionTypes, chosen as the documents somebody hands over or files.
/// Left out on purpose: TRM (a transfer between the business's own bank
/// accounts, which has no counterparty to hand it to), OPB (the migration
/// screen, read-only after go-live), DEP and STA (produced by a job, never
/// printed), and POS — a POS sale is an sal.Invoices row and prints as an
/// ESC/POS receipt from the till, which is not a page layout at all.
///
/// TRM and OPB still carry a PrintTemplateId column, so adding either here
/// later needs code and no migration.
/// </summary>
public static class DocumentTypeCatalog
{
    public static readonly IReadOnlyList<DocumentTypeProfile> All =
    [
        // Sales, in the order one document becomes the next.
        Profile("QTE", "Quote", PartyKind.Customer, LineShape.Item, tax: true, totals: true, payments: false),
        Profile("SOR", "Sales Order", PartyKind.Customer, LineShape.Item, tax: true, totals: true, payments: false),
        Profile("DLC", "Delivery Challan", PartyKind.Customer, LineShape.Item, tax: true, totals: true, payments: false),
        Profile("INV", "Invoice", PartyKind.Customer, LineShape.Item, tax: true, totals: true, payments: true),
        Profile("CRN", "Credit Note", PartyKind.Customer, LineShape.Item, tax: true, totals: true, payments: true),

        // Purchase, likewise.
        Profile("POR", "Purchase Order", PartyKind.Vendor, LineShape.Item, tax: true, totals: true, payments: false),

        // A goods receipt records what arrived, not what it cost — the tax is
        // determined on the bill that follows it.
        Profile("GRN", "Goods Receipt", PartyKind.Vendor, LineShape.Item, tax: false, totals: true, payments: false),
        Profile("BIL", "Bill", PartyKind.Vendor, LineShape.Item, tax: true, totals: true, payments: true),
        Profile("DBN", "Debit Note", PartyKind.Vendor, LineShape.Item, tax: true, totals: true, payments: true),

        // Money documents. Their repeating block is what the money was applied
        // to, not what was sold.
        Profile("RCM", "Receipt", PartyKind.Customer, LineShape.Allocation, tax: false, totals: true, payments: false),
        Profile("SPM", "Payment", PartyKind.Vendor, LineShape.Allocation, tax: false, totals: true, payments: false),

        // A journal has no counterparty and two money columns.
        Profile("JRN", "Journal Voucher", PartyKind.None, LineShape.Ledger, tax: false, totals: true, payments: false),
    ];

    public static DocumentTypeProfile? Find(string? code) =>
        code is null ? null : All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsPrintable(string? code) => Find(code) is not null;

    private static DocumentTypeProfile Profile(
        string code, string name, PartyKind party, LineShape lines, bool tax, bool totals, bool payments) =>
        new()
        {
            Code = code,
            Name = name,
            Party = party,
            Lines = lines,
            HasTax = tax,
            HasTotals = totals,
            HasPayments = payments,
        };
}
