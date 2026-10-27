using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("Bill", Schema = "pur")]
public class BillRead : OrgScopedEntity
{
    public long BillId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string? VendorBillNo { get; set; }
    public DateOnly VendorBillDate { get; set; }
    public long ContactId { get; set; }
    public DateOnly DueDate { get; set; }
    public string CurrencyCode { get; set; } = null!;

    /// <summary>The bill's rate, which multiplies a document amount into base currency.</summary>
    public decimal ExchangeRate { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>The total in the branch's base currency, as the document stored it (TK-235).</summary>
    public decimal TotalAmountBase { get; set; }

    /// <summary>What tax is charged on, in the document's currency (TK-235).</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>The CGST, SGST, IGST and cess, in the document's currency; each is the sum of the document's tax rows (TK-235).</summary>
    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    /// <summary>The tax is paid by the branch itself, not to the vendor, so the vendor is owed the bill less it (TK-235).</summary>
    public bool IsReverseCharge { get; set; }

    /// <summary>TDS withheld from the vendor, in base currency (a bill in another currency carries none); the vendor is owed the bill less it (TK-235).</summary>
    public decimal TdsAmount { get; set; }
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }

    /// <summary>The record's custom field values (TK-111/TK-112), read by the custom report datasets (TK-113).</summary>
    public System.Text.Json.JsonDocument? CustomFields { get; set; }
}
