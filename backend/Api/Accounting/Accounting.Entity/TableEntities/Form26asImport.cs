using Shared.Kernel.Audit;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One Form 26AS text file imported and matched against the TDS customers
/// withheld (TK-117, D-33). The file is kept as it came.
/// </summary>
public class Form26asImport : OrgScopedEntity
{
    public long Form26asImportId { get; set; }

    /// <summary>The April-to-March year the statement covers, named by its first calendar year.</summary>
    public int FinancialYear { get; set; }

    [MaxLength(10, ErrorMessage = "PAN cannot exceed 10 characters.")]
    public string? Pan { get; set; }

    [Required(ErrorMessage = "File name is required.")]
    [MaxLength(260, ErrorMessage = "File name cannot exceed 260 characters.")]
    public string FileName { get; set; } = null!;

    [Required(ErrorMessage = "The file's text is required.")]
    [NotAudited]
    public string RawText { get; set; } = null!;

    public int MatchedCount { get; set; }

    public int DifferenceCount { get; set; }

    public int MissingInBooksCount { get; set; }

    public int MissingIn26asCount { get; set; }

    /// <summary>TDS the books record that 26AS does not support.</summary>
    public decimal CreditAtRisk { get; set; }

    public List<Form26asLine> Lines { get; set; } = [];
}
