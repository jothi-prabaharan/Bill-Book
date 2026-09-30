using Shared.Kernel.Audit;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One GSTR-2B file an accountant imported (TK-116): the month it covers and
/// what matching it against the purchase register found. Who imported it and
/// when are the audit columns.
/// </summary>
public class Gstr2bImport : OrgScopedEntity
{
    public long Gstr2bImportId { get; set; }

    /// <summary>The return period as the portal writes it, <c>MMyyyy</c>.</summary>
    [Required(ErrorMessage = "Return period is required.")]
    [MaxLength(6, ErrorMessage = "Return period must be six digits, MMyyyy.")]
    public string ReturnPeriod { get; set; } = null!;

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    /// <summary>The GSTIN the file was generated for. Refused when it is not the branch's.</summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? RecipientGstin { get; set; }

    [Required(ErrorMessage = "File name is required.")]
    [MaxLength(200, ErrorMessage = "File name cannot exceed 200 characters.")]
    public string FileName { get; set; } = null!;

    /// <summary>The file as imported, kept so a re-match or a dispute can read what the portal said.</summary>
    [Required(ErrorMessage = "The file content is required.")]
    [NotAudited]
    public string RawJson { get; set; } = null!;

    public int MatchedCount { get; set; }

    public int MismatchedCount { get; set; }

    public int MissingInBooksCount { get; set; }

    public int MissingIn2bCount { get; set; }

    /// <summary>Credit claimed in the books that 2B does not support, in base currency.</summary>
    public decimal ItcAtRisk { get; set; }

    public List<Gstr2bLine> Lines { get; set; } = [];
}
