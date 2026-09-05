using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public class PayableInvoiceDetailRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PayableInvoiceDetailSource : ReportSource<PayableInvoiceDetailRow>
{
    public override string ReportKey => "payable-invoice-detail";
    public override string Title => "Payable Invoice Detail";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PayableInvoiceDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo, groupable: true),
        ReportColumn.Of<PayableInvoiceDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PayableInvoiceDetailRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PayableInvoiceDetailRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<PayableInvoiceDetailRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("quantity", ColumnDataType.Number, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceDetailRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false)
    ];

    protected override IQueryable<PayableInvoiceDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from bd in db.BillDetails
               join b in db.Bills on bd.BillId equals b.BillId
               join c in db.Contacts on b.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               join i in db.Items on bd.ItemId equals i.ItemId into gi
               from i in gi.DefaultIfEmpty()
               select new PayableInvoiceDetailRow
               {
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   ContactName = c != null ? c.DisplayName : "Unknown",
                   ItemCode = i != null ? i.ItemCode : null,
                   ItemName = i != null ? i.ItemName : bd.Description,
                   Quantity = bd.Quantity,
                   UnitPrice = bd.UnitPrice,
                   LineTotal = bd.LineTotal
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PayableInvoiceDetailRow, DateOnly>>)(r => r.DocumentDate);
}
