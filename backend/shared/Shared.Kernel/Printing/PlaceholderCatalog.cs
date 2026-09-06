namespace Shared.Kernel.Printing;

/// <summary>One merge field, as the Insert-field panel offers it and the renderer resolves it.</summary>
public sealed class PlaceholderDefinition
{
    /// <summary>What appears between the guillemets in a chip: <c>Item.ItemName</c>.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>The panel's heading, and for a list the collection its rows come from.</summary>
    public string Group { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public PlaceholderType Type { get; init; }

    /// <summary>
    /// <b>Declared, never inferred.</b> A dot in the tag says nothing:
    /// <c>Item.ItemName</c> is a List because Item repeats, while
    /// <c>Organization.Name</c> is Single. Guessing from the dot was tried and
    /// was wrong.
    /// </summary>
    public PlaceholderKind Kind { get; init; }

    /// <summary>Applied server-side against the branch's locale and currency. Null for text.</summary>
    public string? Format { get; init; }

    public int Sort { get; init; }
}

/// <summary>
/// Every merge field the product can resolve, by document type.
///
/// <b>Platform-owned reference data, so it is code rather than a table.</b> A
/// tag only produces a value because a payload builder puts one there; a row a
/// user could add would render empty for ever. Keeping the catalogue beside the
/// renderer means the list the editor offers and the list the renderer honours
/// cannot drift apart.
///
/// <b>Derived, and worth checking.</b> The tags below are taken from what the
/// Sales, Purchase and Accounting entities actually carry.
/// </summary>
public static class PlaceholderCatalog
{
    /// <summary>Amounts: the Indian grouping, two decimals.</summary>
    public const string AmountFormat = "##,##,##0.00";

    /// <summary>Quantities and rates: up to three decimals, trailing ones dropped.</summary>
    public const string NumberFormat = "##,##,##0.0##";

    public const string DateFormat = "DD-MMM-YYYY";

    /// <summary>
    /// The five repeating collections, named exactly as the print payload keys
    /// them. A chip's group is what tells the renderer which list a row comes
    /// from, so these strings are load-bearing.
    /// </summary>
    public static readonly IReadOnlyList<string> ListGroups = ["Item", "Tax", "Payment", "Alloc", "Line"];

    /// <summary>Every placeholder available on one document type, in panel order.</summary>
    public static IReadOnlyList<PlaceholderDefinition> For(string documentTypeCode)
    {
        DocumentTypeProfile? profile = DocumentTypeCatalog.Find(documentTypeCode);
        if (profile is null)
        {
            return [];
        }

        var all = new List<PlaceholderDefinition>();
        all.AddRange(Organization);
        all.AddRange(Document);

        if (profile.Party != PartyKind.None)
        {
            all.AddRange(Party);
        }

        switch (profile.Lines)
        {
            case LineShape.Item: all.AddRange(ItemLines); break;
            case LineShape.Allocation: all.AddRange(AllocationLines); break;
            case LineShape.Ledger: all.AddRange(LedgerLines); break;
        }

        if (profile.HasTax)
        {
            all.AddRange(TaxLines);
        }

        if (profile.HasTotals)
        {
            all.AddRange(Totals);
        }

        if (profile.HasPayments)
        {
            all.AddRange(PaymentLines);
        }

        return all;
    }

    /// <summary>Grouped for the Insert-field panel, group order preserved.</summary>
    public static IReadOnlyList<PlaceholderGroup> Grouped(string documentTypeCode) =>
        [.. For(documentTypeCode)
            .GroupBy(p => p.Group)
            .Select(g => new PlaceholderGroup
            {
                Group = g.Key,
                IsList = g.First().Kind == PlaceholderKind.List,
                Placeholders = [.. g.OrderBy(p => p.Sort)],
            })];

    /// <summary>Looks one tag up for a document type. Null means the renderer must print nothing.</summary>
    public static PlaceholderDefinition? Find(string documentTypeCode, string tag) =>
        For(documentTypeCode).FirstOrDefault(p => string.Equals(p.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static PlaceholderDefinition Single(
        string tag, string group, string description, PlaceholderType type, int sort, string? format = null) =>
        new()
        {
            Tag = tag, Group = group, Description = description,
            Type = type, Kind = PlaceholderKind.Single, Format = format, Sort = sort,
        };

    private static PlaceholderDefinition List(
        string tag, string group, string description, PlaceholderType type, int sort, string? format = null) =>
        new()
        {
            Tag = tag, Group = group, Description = description,
            Type = type, Kind = PlaceholderKind.List, Format = format, Sort = sort,
        };

    private static readonly PlaceholderDefinition[] Organization =
    [
        Single("Organization.Name", "Organization", "Trading name of the branch", PlaceholderType.Text, 10),
        Single("Organization.LegalName", "Organization", "Registered legal name", PlaceholderType.Text, 20),
        Single("Organization.Address", "Organization", "Full branch address", PlaceholderType.RichText, 30),
        Single("Organization.Gstin", "Organization", "GSTIN of the branch", PlaceholderType.Text, 40),
        Single("Organization.StateName", "Organization", "State the branch trades from", PlaceholderType.Text, 50),
        Single("Organization.Phone", "Organization", "Contact telephone", PlaceholderType.Text, 60),
        Single("Organization.Email", "Organization", "Contact email", PlaceholderType.Text, 70),
        Single("Organization.Logo", "Organization", "Branch logo", PlaceholderType.Image, 80),
    ];

    private static readonly PlaceholderDefinition[] Document =
    [
        Single("Document.No", "Document", "Document number", PlaceholderType.Text, 10),
        Single("Document.Date", "Document", "Document date", PlaceholderType.Date, 20, DateFormat),
        Single("Document.DueDate", "Document", "Payment due date", PlaceholderType.Date, 30, DateFormat),
        Single("Document.Reference", "Document", "The other party's own reference", PlaceholderType.Text, 40),
        Single("Document.Status", "Document", "Draft, posted or void", PlaceholderType.Text, 50),
        Single("Document.PlaceOfSupply", "Document", "State the supply is made in", PlaceholderType.Text, 60),
        Single("Document.Currency", "Document", "Currency code", PlaceholderType.Text, 70),
        Single("Document.Notes", "Document", "Notes for the other party", PlaceholderType.RichText, 80),
        Single("Document.Terms", "Document", "Terms and conditions", PlaceholderType.RichText, 90),
    ];

    private static readonly PlaceholderDefinition[] Party =
    [
        Single("Party.Name", "Party", "Customer or vendor name", PlaceholderType.Text, 10),
        Single("Party.Address", "Party", "Billing address", PlaceholderType.RichText, 20),
        Single("Party.ShippingAddress", "Party", "Shipping address", PlaceholderType.RichText, 30),
        Single("Party.Gstin", "Party", "GSTIN of the other party", PlaceholderType.Text, 40),
        Single("Party.StateName", "Party", "State of the other party", PlaceholderType.Text, 50),
        Single("Party.Phone", "Party", "Telephone", PlaceholderType.Text, 60),
        Single("Party.Email", "Party", "Email", PlaceholderType.Text, 70),
    ];

    private static readonly PlaceholderDefinition[] ItemLines =
    [
        List("Item.SlNo", "Item", "Line number", PlaceholderType.Number, 10),
        List("Item.ItemName", "Item", "Item name", PlaceholderType.Text, 20),
        List("Item.Description", "Item", "Line description", PlaceholderType.Text, 30),
        List("Item.HsnSac", "Item", "HSN or SAC code", PlaceholderType.Text, 40),
        List("Item.Quantity", "Item", "Quantity", PlaceholderType.Number, 50, NumberFormat),
        List("Item.Uom", "Item", "Unit of measure", PlaceholderType.Text, 60),
        List("Item.Rate", "Item", "Unit rate", PlaceholderType.Amount, 70, AmountFormat),
        List("Item.Discount", "Item", "Discount on the line", PlaceholderType.Amount, 80, AmountFormat),
        List("Item.TaxableValue", "Item", "Value tax is charged on", PlaceholderType.Amount, 90, AmountFormat),
        List("Item.TaxRate", "Item", "Combined tax rate", PlaceholderType.Number, 100, NumberFormat),
        List("Item.Amount", "Item", "Line total", PlaceholderType.Amount, 110, AmountFormat),
    ];

    private static readonly PlaceholderDefinition[] TaxLines =
    [
        List("Tax.Component", "Tax", "CGST, SGST or IGST", PlaceholderType.Text, 10),
        List("Tax.Rate", "Tax", "Rate of the component", PlaceholderType.Number, 20, NumberFormat),
        List("Tax.TaxableValue", "Tax", "Value taxed at this rate", PlaceholderType.Amount, 30, AmountFormat),
        List("Tax.Amount", "Tax", "Tax charged", PlaceholderType.Amount, 40, AmountFormat),
    ];

    private static readonly PlaceholderDefinition[] AllocationLines =
    [
        List("Alloc.DocumentNo", "Alloc", "Document the money was applied to", PlaceholderType.Text, 10),
        List("Alloc.DocumentDate", "Alloc", "Date of that document", PlaceholderType.Date, 20, DateFormat),
        List("Alloc.DocumentTotal", "Alloc", "What that document was for", PlaceholderType.Amount, 30, AmountFormat),
        List("Alloc.Amount", "Alloc", "Amount applied to it", PlaceholderType.Amount, 40, AmountFormat),
    ];

    private static readonly PlaceholderDefinition[] LedgerLines =
    [
        List("Line.AccountCode", "Line", "Account code", PlaceholderType.Text, 10),
        List("Line.AccountName", "Line", "Account name", PlaceholderType.Text, 20),
        List("Line.Description", "Line", "Line narration", PlaceholderType.Text, 30),
        List("Line.Debit", "Line", "Debit amount", PlaceholderType.Amount, 40, AmountFormat),
        List("Line.Credit", "Line", "Credit amount", PlaceholderType.Amount, 50, AmountFormat),
    ];

    private static readonly PlaceholderDefinition[] PaymentLines =
    [
        List("Payment.Date", "Payment", "Date received or paid", PlaceholderType.Date, 10, DateFormat),
        List("Payment.Mode", "Payment", "Cash, bank or card", PlaceholderType.Text, 20),
        List("Payment.Reference", "Payment", "Cheque or transaction reference", PlaceholderType.Text, 30),
        List("Payment.Amount", "Payment", "Amount", PlaceholderType.Amount, 40, AmountFormat),
    ];

    private static readonly PlaceholderDefinition[] Totals =
    [
        Single("Totals.SubTotal", "Totals", "Before tax and discount", PlaceholderType.Amount, 10, AmountFormat),
        Single("Totals.Discount", "Totals", "Total discount", PlaceholderType.Amount, 20, AmountFormat),
        Single("Totals.TaxableValue", "Totals", "Total value tax was charged on", PlaceholderType.Amount, 30, AmountFormat),
        Single("Totals.Tax", "Totals", "Total tax", PlaceholderType.Amount, 40, AmountFormat),
        Single("Totals.RoundOff", "Totals", "Rounding adjustment", PlaceholderType.Amount, 50, AmountFormat),
        Single("Totals.GrandTotal", "Totals", "Amount payable", PlaceholderType.Amount, 60, AmountFormat),
        Single("Totals.AmountInWords", "Totals", "Grand total spelled out", PlaceholderType.Text, 70),
        Single("Totals.Paid", "Totals", "Already settled", PlaceholderType.Amount, 80, AmountFormat),
        Single("Totals.Outstanding", "Totals", "Still owing", PlaceholderType.Amount, 90, AmountFormat),
    ];
}

/// <summary>One heading in the Insert-field panel.</summary>
public sealed class PlaceholderGroup
{
    public string Group { get; init; } = string.Empty;

    /// <summary>Flagged so the panel can mark the repeat icon on every tag in it.</summary>
    public bool IsList { get; init; }

    public IReadOnlyList<PlaceholderDefinition> Placeholders { get; init; } = [];
}
