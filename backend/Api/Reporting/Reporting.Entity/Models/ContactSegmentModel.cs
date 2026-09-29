namespace Reporting.Entity.Models;

public sealed class ContactSegmentRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public DateOnly? LastInvoiceBefore { get; set; }
    public DateOnly? LastInvoiceAfter { get; set; }
    public decimal? SalesMinAmount { get; set; }
    public decimal? SalesMaxAmount { get; set; }
    public DateOnly? SalesPeriodFrom { get; set; }
    public DateOnly? SalesPeriodTo { get; set; }
    public long? ItemCategoryId { get; set; }
}

public sealed class ContactSegmentResponse
{
    public List<long> ContactIds { get; set; } = [];
}
