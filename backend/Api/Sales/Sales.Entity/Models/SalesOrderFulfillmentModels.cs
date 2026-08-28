using System.ComponentModel.DataAnnotations;

namespace Sales.Entity.Models;

/// <summary>
/// Requests fulfillment of some or all remaining Sales Order quantities by invoice.
/// Empty Lines means all currently uninvoiced lines.
/// </summary>
public sealed class FulfillSalesOrderRequest
{
    public DateOnly? DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? PaymentTermId { get; set; }

    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? Notes { get; set; }

    public List<FulfillSalesOrderLineRequest> Lines { get; set; } = [];
}

/// <summary>One order line and the quantity to invoice/issue now.</summary>
public sealed class FulfillSalesOrderLineRequest
{
    [Range(1, long.MaxValue)]
    public long SalesOrderDetailId { get; set; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335", ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }
}

public sealed class FulfillSalesOrderResult
{
    public long SalesOrderId { get; set; }
    public long InvoiceId { get; set; }
    public string Status { get; set; } = null!;
    public List<FulfilledSalesOrderLine> Lines { get; set; } = [];
}

public sealed class FulfilledSalesOrderLine
{
    public long SalesOrderDetailId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal PreviouslyInvoicedQuantity { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
}
