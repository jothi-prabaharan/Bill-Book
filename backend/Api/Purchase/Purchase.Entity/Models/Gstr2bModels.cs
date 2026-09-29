using Purchase.Entity.Enums;

namespace Purchase.Entity.Models;

/// <summary>A GSTR-2B import as the list shows it (TK-116).</summary>
public class Gstr2bImportView
{
    public long Gstr2bImportId { get; set; }

    public string ReturnPeriod { get; set; } = null!;

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public string FileName { get; set; } = null!;

    public DateTimeOffset? ImportedAt { get; set; }

    public int MatchedCount { get; set; }

    public int MismatchedCount { get; set; }

    public int MissingInBooksCount { get; set; }

    public int MissingIn2bCount { get; set; }

    public decimal ItcAtRisk { get; set; }
}

/// <summary>One import with its lines.</summary>
public class Gstr2bImportDetail : Gstr2bImportView
{
    public List<Gstr2bLineView> Lines { get; set; } = [];
}

public class Gstr2bLineView
{
    public long Gstr2bLineId { get; set; }

    public Gstr2bMatchState MatchState { get; set; }

    public string SupplierGstin { get; set; } = null!;

    public string? SupplierName { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public DateOnly InvoiceDate { get; set; }

    public bool ReverseCharge { get; set; }

    public bool? ItcAvailable { get; set; }

    public decimal? TwoBTaxable { get; set; }

    public decimal? TwoBTax { get; set; }

    public decimal? BooksTaxable { get; set; }

    public decimal? BooksTax { get; set; }

    public long? BillId { get; set; }

    public string? BillNo { get; set; }

    public string? Note { get; set; }
}

public enum Gstr2bImportOutcome
{
    Ok = 0,

    /// <summary>The file is not a GSTR-2B the importer can read; the detail says why.</summary>
    Unreadable = 1,

    /// <summary>The file was generated for another GSTIN.</summary>
    WrongGstin = 2,
}

public sealed record Gstr2bImportResult(Gstr2bImportOutcome Outcome, long Gstr2bImportId = 0, string? Detail = null);
