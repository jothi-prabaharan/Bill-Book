using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("Invoice", Schema = "sal")]
public class InvoiceRead : OrgScopedEntity
{
    public long InvoiceId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string CurrencyCode { get; set; } = null!;

    /// <summary>Snapshot at the document date (TK-113 reads it to put line amounts in base currency).</summary>
    public decimal ExchangeRate { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>The total in the branch's base currency, as the document stored it.</summary>
    public decimal TotalAmountBase { get; set; }
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }

    /// <summary>The record's custom field values (TK-111/TK-112), read by the custom report datasets (TK-113).</summary>
    public System.Text.Json.JsonDocument? CustomFields { get; set; }
}
