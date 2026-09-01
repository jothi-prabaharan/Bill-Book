using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class InvoiceDetailRead : OrgScopedEntity
{
    public long InvoiceDetailId { get; set; }
    public long InvoiceId { get; set; }
    public long? SalesOrderDetailId { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public long? StockMovementId { get; set; }
    public decimal UnitCost { get; set; }

    public int LineNumber { get; set; }
    public long? ItemId { get; set; }
    public string? HsnSacCode { get; set; }
    public string? Description { get; set; }
    public long? WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public long? UomId { get; set; }
    public decimal ConversionFactor { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsPriceInclusive { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public Shared.Kernel.Documents.TaxTreatment TaxTreatment { get; set; }
    public long? TaxMasterId { get; set; }
    public long? TaxGroupId { get; set; }
    public decimal TaxAmount { get; set; }
    public Shared.Kernel.Documents.DocumentLineType LineType { get; set; }
    public long? AccountId { get; set; }
    public long? FixedAssetCategoryId { get; set; }
    public decimal LineTotal { get; set; }
    public long? ItemBatchId { get; set; }
    public string? LineNotes { get; set; }
}
