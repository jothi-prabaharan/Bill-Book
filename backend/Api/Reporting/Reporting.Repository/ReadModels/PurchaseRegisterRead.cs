#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>pur.PurchaseRegister</c>, read-only (TK-115): one row per posted bill
/// or debit note line, with its reverse-charge flag and whether its credit can
/// be claimed. What GSTR-3B's input tax credit is added up from. Amounts are
/// positive for a debit note too; its type says which way it counts.
/// </summary>
public class PurchaseRegisterRead : OrgScopedEntity
{
    public long PurchaseRegisterId { get; set; }

    /// <summary><c>BIL</c> or <c>DBN</c>.</summary>
    public string TransactionTypeCode { get; set; }

    public long SourceId { get; set; }

    public long SourceLineId { get; set; }

    public string DocumentNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    public string SupplierInvoiceNo { get; set; }

    public DateOnly SupplierInvoiceDate { get; set; }

    public long ContactId { get; set; }

    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public bool IsInterState { get; set; }

    public bool ReverseCharge { get; set; }

    /// <summary><c>Eligible</c>, <c>Ineligible</c> or <c>Blocked</c>, as Purchase stores it.</summary>
    public string ItcEligibility { get; set; }

    /// <summary><c>Stock</c>, <c>Expense</c> or <c>Capital</c>.</summary>
    public string LineType { get; set; }

    public string? HsnSacCode { get; set; }

    public decimal GstRate { get; set; }

    public decimal Quantity { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal TaxableAmountBase { get; set; }

    public decimal TaxAmountBase { get; set; }

    public long? OriginalBillId { get; set; }

    public string? OriginalBillNo { get; set; }
}
