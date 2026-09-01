import os

base_path = r'c:\Users\Praba\Source\repos\Bill-Book\backend\Api\Reporting\Reporting.Repository\ReadModels'

header_props = '''
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public string? ContactGstin { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }
    public int PlaceOfSupplyStateId { get; set; }
    public bool IsInterState { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalAmountBase { get; set; }
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
'''

line_props = '''
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
'''

models = {
    'SalesOrderRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class SalesOrderRead : OrgScopedEntity
{{
    public long SalesOrderId {{ get; set; }}
    public long? QuoteId {{ get; set; }}
    public DateOnly? DeliveryDate {{ get; set; }}
    public int FulfilmentStatus {{ get; set; }} // enum mapped as int
    public string? ShortCloseReason {{ get; set; }}
{header_props}}}
''',
    'SalesOrderDetailRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class SalesOrderDetailRead : OrgScopedEntity
{{
    public long SalesOrderDetailId {{ get; set; }}
    public long SalesOrderId {{ get; set; }}
    public decimal ReservedQuantity {{ get; set; }}
    public decimal DeliveredQuantity {{ get; set; }}
{line_props}}}
''',
    'QuoteRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class QuoteRead : OrgScopedEntity
{{
    public long QuoteId {{ get; set; }}
    public DateOnly ValidUntil {{ get; set; }}
{header_props}}}
''',
    'DeliveryChallanRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class DeliveryChallanRead : OrgScopedEntity
{{
    public long DeliveryChallanId {{ get; set; }}
    public long? SalesOrderId {{ get; set; }}
    public int ChallanType {{ get; set; }} // enum mapped as int
    public DateOnly DispatchDate {{ get; set; }}
    public string? VehicleNo {{ get; set; }}
    public string? TransporterName {{ get; set; }}
    public string? EwayBillNo {{ get; set; }}
    public DateOnly? EwayBillDate {{ get; set; }}
{header_props}}}
''',
    'InvoiceDetailRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class InvoiceDetailRead : OrgScopedEntity
{{
    public long InvoiceDetailId {{ get; set; }}
    public long InvoiceId {{ get; set; }}
    public long? SalesOrderDetailId {{ get; set; }}
    public decimal ReturnedQuantity {{ get; set; }}
    public long? StockMovementId {{ get; set; }}
    public decimal UnitCost {{ get; set; }}
{line_props}}}
''',
    'PurchaseOrderRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class PurchaseOrderRead : OrgScopedEntity
{{
    public long PurchaseOrderId {{ get; set; }}
    public DateOnly? ExpectedDate {{ get; set; }}
    public int FulfilmentStatus {{ get; set; }} // enum mapped as int
{header_props}}}
''',
    'GoodsReceiptRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class GoodsReceiptRead : OrgScopedEntity
{{
    public long GoodsReceiptId {{ get; set; }}
    public long? PurchaseOrderId {{ get; set; }}
    public string? VendorDeliveryNoteNo {{ get; set; }}
    public DateOnly? VendorDeliveryNoteDate {{ get; set; }}
    public Guid? ReceivedBy {{ get; set; }}
{header_props}}}
''',
    'BillDetailRead': f'''using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class BillDetailRead : OrgScopedEntity
{{
    public long BillDetailId {{ get; set; }}
    public long BillId {{ get; set; }}
    public long? GoodsReceiptDetailId {{ get; set; }}
    public long? PurchaseOrderDetailId {{ get; set; }}
    public decimal ApportionedLandedCost {{ get; set; }}
    public decimal ReturnedQuantity {{ get; set; }}
{line_props}}}
'''
}

for name, content in models.items():
    with open(os.path.join(base_path, name + '.cs'), 'w') as f:
        f.write(content)
