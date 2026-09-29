using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// The TDS register (TK-117): one row per document that counts toward a
/// vendor's threshold or carries a deduction, whichever side of it the branch
/// is on. Written in the posting transaction and removed with a void.
///
/// <b>Rows below the threshold are kept too</b>, with a zero deduction,
/// because the year's aggregate is their sum: the bill that crosses the
/// threshold is taxed on what went before it. The 26Q export reads the
/// deducted rows; the 26AS match reads the suffered ones.
///
/// Amounts are in base currency. A foreign-currency vendor is paid under
/// section 195 and returned on 27Q, neither of which is built, so such a
/// document writes no row.
/// </summary>
public class TdsEntry : OrgScopedEntity
{
    public long TdsEntryId { get; set; }

    public TdsDirection Direction { get; set; }

    public TdsEntryKind Kind { get; set; }

    /// <summary>BIL, SPM or RCM, with <see cref="TransactionId"/> the document. One row per document.</summary>
    [Required(ErrorMessage = "Transaction type is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    [MaxLength(50, ErrorMessage = "Document number cannot exceed 50 characters.")]
    public string? DocumentNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    /// <summary>The April-to-March year the document falls in, named by its first calendar year.</summary>
    public int FinancialYear { get; set; }

    /// <summary>Unenforced reference to con.Contacts, in another service.</summary>
    public long ContactId { get; set; }

    [MaxLength(10, ErrorMessage = "Section code cannot exceed 10 characters.")]
    public string? SectionCode { get; set; }

    /// <summary>The deductee's PAN when the branch deducted.</summary>
    [MaxLength(10, ErrorMessage = "PAN cannot exceed 10 characters.")]
    public string? Pan { get; set; }

    /// <summary>The deductor's TAN when a customer deducted.</summary>
    [MaxLength(10, ErrorMessage = "TAN cannot exceed 10 characters.")]
    public string? Tan { get; set; }

    public TdsDeducteeKind? DeducteeKind { get; set; }

    /// <summary>What the document credited or paid, before GST.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The part of <see cref="Amount"/> an earlier advance already bore TDS on,
    /// so it is not taxed twice. Only on a <see cref="TdsEntryKind.Credit"/>.
    /// </summary>
    public decimal CoveredByAdvance { get; set; }

    /// <summary>What the deduction was computed on — the catch-up on the crossing document included.</summary>
    public decimal TdsBase { get; set; }

    /// <summary>The section rate applied to the part of the base outside a certificate, in percent.</summary>
    public decimal Rate { get; set; }

    [MaxLength(20, ErrorMessage = "Certificate number cannot exceed 20 characters.")]
    public string? CertificateNo { get; set; }

    /// <summary>The part of the base taxed at the lower-deduction certificate's rate.</summary>
    public decimal CertificateBase { get; set; }

    public decimal? CertificateRate { get; set; }

    public decimal TdsAmount { get; set; }

    /// <summary>The challan (a spend-money document) that paid this deduction to the government.</summary>
    public long? ChallanSpendMoneyId { get; set; }

    /// <summary>Why the figure is what it is, for the document's screen.</summary>
    [MaxLength(300, ErrorMessage = "Note cannot exceed 300 characters.")]
    public string? Note { get; set; }
}
