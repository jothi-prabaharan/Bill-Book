namespace Accounting.Entity.Models;

/// <summary>
/// A tax rate a document line may take: active and in force on the document's
/// date (TK-192). The split is the master's own, so the page shows the CGST,
/// SGST and IGST that posting will use rather than working them out again.
/// </summary>
public class TaxRateChoice
{
    public long TaxMasterId { get; set; }

    public long TaxGroupId { get; set; }

    public string TaxName { get; set; } = null!;

    public decimal TotalRate { get; set; }

    public decimal CgstRate { get; set; }

    public decimal SgstRate { get; set; }

    public decimal IgstRate { get; set; }

    public decimal CessRate { get; set; }
}

/// <summary>A payment term a document or contact may take (TK-192).</summary>
public class PaymentTermChoice
{
    public long PaymentTermId { get; set; }

    public string TermName { get; set; } = null!;

    public string TermType { get; set; } = null!;

    public int DueDays { get; set; }

    public int? DueDayOfMonth { get; set; }

    /// <summary>The branch's preselected term.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>An account a document line may post to (TK-192).</summary>
public class AccountChoice
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public int AccountTypeId { get; set; }

    public string? CurrencyCode { get; set; }
    public long? ParentAccountId { get; set; }
    public string? ParentAccountName { get; set; }
    public int Depth { get; set; }
}

/// <summary>
/// The number the next document in a series will probably take (TK-192). Only
/// probably: the number is allocated when the document is saved, so another
/// user saving first takes this one. Null when the branch has no active series
/// for the code.
/// </summary>
public class NextNumberChoice
{
    public string SeriesCode { get; set; } = null!;

    public string? Number { get; set; }
}
