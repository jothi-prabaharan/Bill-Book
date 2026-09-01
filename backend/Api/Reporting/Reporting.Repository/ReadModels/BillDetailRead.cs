using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class BillDetailRead : OrgScopedEntity
{
    public long BillDetailId { get; set; }
    public long BillId { get; set; }
    public long? GoodsReceiptDetailId { get; set; }
    public long? PurchaseOrderDetailId { get; set; }
    public decimal ApportionedLandedCost { get; set; }
    public decimal ReturnedQuantity { get; set; }

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
