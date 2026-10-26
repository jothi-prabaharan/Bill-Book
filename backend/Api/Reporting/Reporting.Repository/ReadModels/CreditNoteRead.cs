using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>sal.CreditNotes</c>, read-only: what the portal's trade value subtracts
/// from invoices (TK-95).
/// </summary>
public class CreditNoteRead : OrgScopedEntity
{
    public long CreditNoteId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public string CurrencyCode { get; set; } = null!;

    /// <summary>Snapshot at the document date, base currency per unit of <see cref="CurrencyCode"/> (TK-224).</summary>
    public decimal ExchangeRate { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalAmountBase { get; set; }

    /// <summary>What tax is charged on, in the document's currency (TK-224).</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>The CGST, SGST, IGST and cess, in the document's currency (TK-224).</summary>
    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }

    /// <summary>Why it was raised; only a sales return brings goods back (TK-228).</summary>
    public Reporting.Entity.Enums.CreditNoteReason ReasonCode { get; set; }
}
