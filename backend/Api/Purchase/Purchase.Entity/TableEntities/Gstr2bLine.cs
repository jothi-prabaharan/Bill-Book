using Shared.Kernel.Audit;
using System.ComponentModel.DataAnnotations;
using Purchase.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One supplier invoice in a reconciliation (TK-116): as 2B reported it, as
/// the books hold it, or both, and what comparing them found. A bill 2B does
/// not mention is a line too, with no 2B figures, so the screen reads one table.
/// </summary>
[NotAudited]
public class Gstr2bLine : OrgScopedEntity
{
    public long Gstr2bLineId { get; set; }

    public long Gstr2bImportId { get; set; }

    public Gstr2bMatchState MatchState { get; set; }

    [Required(ErrorMessage = "Supplier GSTIN is required.")]
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string SupplierGstin { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Supplier name cannot exceed 200 characters.")]
    public string? SupplierName { get; set; }

    /// <summary>The invoice number, as 2B has it, or as the bill has it when 2B has none.</summary>
    [Required(ErrorMessage = "Invoice number is required.")]
    [MaxLength(50, ErrorMessage = "Invoice number cannot exceed 50 characters.")]
    public string InvoiceNo { get; set; } = null!;

    public DateOnly InvoiceDate { get; set; }

    public bool ReverseCharge { get; set; }

    /// <summary>2B's own "ITC available" flag. Null for a line only in the books.</summary>
    public bool? ItcAvailable { get; set; }

    public decimal? TwoBTaxable { get; set; }

    public decimal? TwoBTax { get; set; }

    public decimal? BooksTaxable { get; set; }

    public decimal? BooksTax { get; set; }

    /// <summary>The bill it matched, or that 2B is missing.</summary>
    public long? BillId { get; set; }

    [MaxLength(30, ErrorMessage = "Bill number cannot exceed 30 characters.")]
    public string? BillNo { get; set; }

    /// <summary>
    /// Why it matched loosely or did not match: the number agreed only once case,
    /// separators and leading zeros were ignored, or the date or values differ.
    /// </summary>
    [MaxLength(300, ErrorMessage = "Note cannot exceed 300 characters.")]
    public string? Note { get; set; }
}
