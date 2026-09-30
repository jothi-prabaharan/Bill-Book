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

public sealed class ContactFirstInvoicesRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public List<long> ContactIds { get; set; } = [];
}

public sealed class ContactFirstInvoiceDto
{
    public long ContactId { get; set; }
    public DateOnly FirstInvoiceDate { get; set; }
    public decimal FirstInvoiceTotal { get; set; }
}

public sealed class ContactFirstInvoicesResponse
{
    public List<ContactFirstInvoiceDto> FirstInvoices { get; set; } = [];
}
