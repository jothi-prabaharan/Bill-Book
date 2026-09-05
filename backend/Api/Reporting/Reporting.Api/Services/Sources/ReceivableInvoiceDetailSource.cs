using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class ReceivableInvoiceDetailRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class ReceivableInvoiceDetailSource : ReportSource<ReceivableInvoiceDetailRow>
{
    public override string ReportKey => "receivable-invoice-detail";

    public override string Title => "Receivable Invoice Detail";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReceivableInvoiceDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<ReceivableInvoiceDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ReceivableInvoiceDetailRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("quantity", ColumnDataType.Number, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("unitPrice", ColumnDataType.Number, r => r.UnitPrice),
        ReportColumn.Of<ReceivableInvoiceDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<ReceivableInvoiceDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from i in db.Invoices
               join d in db.InvoiceDetails on i.InvoiceId equals d.InvoiceId
               join c in db.Contacts on i.ContactId equals c.ContactId
               join it in db.Items on d.ItemId equals it.ItemId into itemGroup
               from it in itemGroup.DefaultIfEmpty()
               select new ReceivableInvoiceDetailRow
               {
                   DocumentNo = i.DocumentNo,
                   DocumentDate = i.DocumentDate,
                   ContactName = c.DisplayName,
                   ItemCode = it != null ? it.ItemCode : null,
                   ItemName = it != null ? it.ItemName : d.Description, // Fallback to description if no item
                   Quantity = d.Quantity,
                   UnitPrice = d.UnitPrice,
                   LineTotal = d.LineTotal
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceivableInvoiceDetailRow, DateOnly>>)(r => r.DocumentDate);
}
