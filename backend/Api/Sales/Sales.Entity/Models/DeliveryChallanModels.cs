using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;

namespace Sales.Entity.Models;

public class DeliveryChallanListItem
{
    public long DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public DocumentStatus Status { get; set; }
    public DateOnly DispatchDate { get; set; }
    public decimal TotalAmount { get; set; }
}

public class DeliveryChallanView
{
    public long DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public DocumentStatus Status { get; set; }
    public ChallanType ChallanType { get; set; } = ChallanType.Sale;
    public string? VehicleNo { get; set; }
    public string? TransporterName { get; set; }
    public string? EwayBillNo { get; set; }
    public DateOnly? EwayBillDate { get; set; }
    public DateOnly DispatchDate { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }

    public string? Notes { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    public int PlaceOfSupplyStateId { get; set; }
    public bool IsInterState { get; set; }

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

    public List<DeliveryChallanLineView> Lines { get; set; } = [];
}

public class DeliveryChallanLineView
{
    public long DeliveryChallanDetailId { get; set; }
    public long? ItemId { get; set; }
    public string? ItemLabel { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }

    public List<DeliveryChallanLineTaxView> Taxes { get; set; } = [];
}

public class DeliveryChallanLineTaxView
{
    public TaxComponent TaxComponent { get; set; }
    public long SubAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class SaveDeliveryChallanRequest
{
    public long? DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    
    [Required]
    public DateOnly DocumentDate { get; set; }
    
    [Range(1, long.MaxValue, ErrorMessage = "Contact is required")]
    public long ContactId { get; set; }

    public ChallanType ChallanType { get; set; } = ChallanType.Sale;
    public string? VehicleNo { get; set; }
    public string? TransporterName { get; set; }
    public string? EwayBillNo { get; set; }
    public DateOnly? EwayBillDate { get; set; }
    public DateOnly DispatchDate { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
    
    public decimal ExchangeRate { get; set; } = 1m;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? BillingAddress { get; set; }

    [MaxLength(100)]
    public string? ShippingAddress { get; set; }

    public List<SaveDeliveryChallanLineRequest> Lines { get; set; } = [];
}

public class SaveDeliveryChallanLineRequest
{
    public long ItemId { get; set; }
    
    [Range(0.000001, double.MaxValue)]
    public decimal Quantity { get; set; }
    
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    
    public List<long> TaxGroupIds { get; set; } = [];
}
