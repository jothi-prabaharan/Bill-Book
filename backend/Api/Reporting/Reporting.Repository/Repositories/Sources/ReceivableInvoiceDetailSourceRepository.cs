using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

public class ReceivableInvoiceDetailRow
{
    public long InvoiceDetailId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// The Receivable Invoice Detail for export, saved views and scheduled emails (TK-225). One row for each
/// line of each posted invoice dated in the period, the lines the page lists. <b>Both dates are required</b>
/// (D-67). The unit price is in the invoice's own currency and the line total in base currency, so the
/// column can be added up; they are the same on a branch that invoices in its own currency. A voided
/// invoice and a draft are not listed.
/// </summary>
public sealed class ReceivableInvoiceDetailSourceRepository : ReportSource<ReceivableInvoiceDetailRow>
{
    public override string ReportKey => "receivable-invoice-detail";

    public override string Title => "Receivable Invoice Detail";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReceivableInvoiceDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<ReceivableInvoiceDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<ReceivableInvoiceDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        return from i in db.Invoices
               where i.Status == DocumentStatus.Posted && i.DocumentDate >= start && i.DocumentDate <= end
               join d in db.InvoiceDetails on i.InvoiceId equals d.InvoiceId
               join c in db.Contacts on i.ContactId equals c.ContactId
               join it in db.Items on d.ItemId equals (long?)it.ItemId into itemGroup
               from it in itemGroup.DefaultIfEmpty()
               select new ReceivableInvoiceDetailRow
               {
                   InvoiceDetailId = d.InvoiceDetailId,
                   DocumentNo = i.DocumentNo,
                   DocumentDate = i.DocumentDate,
                   ContactName = c.DisplayName,
                   ItemCode = it != null ? it.ItemCode : null,
                   ItemName = it != null ? it.ItemName : d.Description, // Fallback to description if no item
                   Quantity = d.Quantity,
                   UnitPrice = d.UnitPrice,
                   LineTotal = Math.Round(d.LineTotal * i.ExchangeRate, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceivableInvoiceDetailRow, long>>)(r => r.InvoiceDetailId);
}
