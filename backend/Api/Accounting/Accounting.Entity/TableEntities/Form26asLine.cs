using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One deductor's figure for the year, from 26AS or from the books, and how
/// the two compare. Matching is by TAN and amount: 26AS reports a deductor's
/// transactions, and a receipt records the TDS withheld from it.
/// </summary>
public class Form26asLine : OrgScopedEntity
{
    public long Form26asLineId { get; set; }

    public long Form26asImportId { get; set; }

    public Form26asMatchState MatchState { get; set; }

    [Required(ErrorMessage = "TAN is required.")]
    [MaxLength(10, ErrorMessage = "TAN cannot exceed 10 characters.")]
    public string DeductorTan { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Deductor name cannot exceed 200 characters.")]
    public string? DeductorName { get; set; }

    [MaxLength(10, ErrorMessage = "Section code cannot exceed 10 characters.")]
    public string? SectionCode { get; set; }

    public DateOnly? TransactionDate { get; set; }

    /// <summary>What 26AS says was paid or credited.</summary>
    public decimal? StatementAmountPaid { get; set; }

    /// <summary>What 26AS says was deducted.</summary>
    public decimal? StatementTds { get; set; }

    /// <summary>What the books say was withheld, from the matched receipts.</summary>
    public decimal? BooksTds { get; set; }

    /// <summary>The receipt, when one matched.</summary>
    public long? ReceiveMoneyId { get; set; }

    [MaxLength(50, ErrorMessage = "Document number cannot exceed 50 characters.")]
    public string? DocumentNo { get; set; }

    [MaxLength(300, ErrorMessage = "Note cannot exceed 300 characters.")]
    public string? Note { get; set; }
}
