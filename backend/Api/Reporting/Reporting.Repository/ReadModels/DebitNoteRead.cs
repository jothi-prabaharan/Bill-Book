using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>pur.DebitNotes</c>, read-only (TK-235): what a vendor is given back, which the Payable Invoice Summary lists
/// as a negative beside the bills when asked. A debit note names the bill it corrects.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("DebitNote", Schema = "pur")]
public class DebitNoteRead : OrgScopedEntity
{
    public long DebitNoteId { get; set; }

    /// <summary>The bill being corrected.</summary>
    public long BillId { get; set; }

    public string TransactionTypeCode { get; set; } = null!;

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = null!;

    /// <summary>Snapshot at the document date, base currency per unit of <see cref="CurrencyCode"/>.</summary>
    public decimal ExchangeRate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TotalAmountBase { get; set; }

    /// <summary>What tax is charged on, in the document's currency.</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>The CGST, SGST, IGST and cess, in the document's currency.</summary>
    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
}
