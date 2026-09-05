using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public class PurchaseReceiveOrderDetailsRow
{
    public long GoodsReceiptId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class PurchaseReceiveOrderDetailsSource : ReportSource<PurchaseReceiveOrderDetailsRow>
{
    public override string ReportKey => "purchase-receive-order-details";
    public override string Title => "Purchase Receive Order Details";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo, groupable: true),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, decimal>("quantity", ColumnDataType.Number, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, long>("goodsReceiptId", ColumnDataType.Number, r => r.GoodsReceiptId, filterable: false)
    ];

    protected override IQueryable<PurchaseReceiveOrderDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from gr in db.GoodsReceipts
               join grd in db.Set<GoodsReceiptDetailRead>() on gr.GoodsReceiptId equals grd.GoodsReceiptId
               join c in db.Contacts on gr.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               join i in db.Items on grd.ItemId equals i.ItemId into gi
               from i in gi.DefaultIfEmpty()
               select new PurchaseReceiveOrderDetailsRow
               {
                   GoodsReceiptId = gr.GoodsReceiptId,
                   DocumentNo = gr.DocumentNo,
                   DocumentDate = gr.DocumentDate,
                   ContactName = c != null ? c.DisplayName : "Unknown",
                   ItemCode = i != null ? i.ItemCode : null,
                   Quantity = grd.Quantity
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseReceiveOrderDetailsRow, DateOnly>>)(r => r.DocumentDate);
}
