using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class PurchaseReceiveOrderDetailsRow
{
    public long GoodsReceiptId { get; set; }
    public long GoodsReceiptDetailId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// The Purchase Receive Order Details for export, saved views and scheduled emails (TK-241). One row
/// for each line of a posted goods receipt dated in the period, from the builders the page uses
/// (<see cref="PurchaseReceiveOrderDetailsReportRepository.Receipts"/>): the quantity the vendor
/// delivered, what was accepted into stock and what was rejected, and the accepted goods' value in the
/// branch's base currency. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class PurchaseReceiveOrderDetailsSourceRepository : ReportSource<PurchaseReceiveOrderDetailsRow>
{
    public override string ReportKey => "purchase-receive-order-details";
    public override string Title => "Purchase Receive Order Details";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo, groupable: true),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, decimal>("acceptedQuantity", ColumnDataType.Quantity, r => r.AcceptedQuantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, decimal>("rejectedQuantity", ColumnDataType.Quantity, r => r.RejectedQuantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, decimal>("value", ColumnDataType.Money, r => r.Value, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, long>("goodsReceiptId", ColumnDataType.Number, r => r.GoodsReceiptId, filterable: false),
        ReportColumn.Of<PurchaseReceiveOrderDetailsRow, long>("goodsReceiptDetailId", ColumnDataType.Number, r => r.GoodsReceiptDetailId, filterable: false),
    ];

    protected override IQueryable<PurchaseReceiveOrderDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new PurchaseReceiveOrderDetailsReportRequest { FromDate = start, ToDate = end };
        IQueryable<GoodsReceiptRead> receipts = PurchaseReceiveOrderDetailsReportRepository.Receipts(db.GoodsReceipts, request, start, end);

        return from gr in receipts
               join d in db.GoodsReceiptDetails on gr.GoodsReceiptId equals d.GoodsReceiptId
               join c in db.Contacts on gr.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               join i in db.Items on d.ItemId equals (long?)i.ItemId into gi
               from i in gi.DefaultIfEmpty()
               select new PurchaseReceiveOrderDetailsRow
               {
                   GoodsReceiptId = gr.GoodsReceiptId,
                   GoodsReceiptDetailId = d.GoodsReceiptDetailId,
                   DocumentNo = gr.DocumentNo,
                   DocumentDate = gr.DocumentDate,
                   ContactName = c != null ? c.DisplayName : "Unknown",
                   ItemCode = i != null ? i.ItemCode : null,
                   Quantity = d.Quantity,
                   AcceptedQuantity = d.AcceptedQuantity,
                   RejectedQuantity = d.RejectedQuantity,
                   Value = d.Quantity == 0m ? 0m : Math.Round(Math.Round(d.TaxableAmount * d.AcceptedQuantity / d.Quantity, 2) * gr.ExchangeRate, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseReceiveOrderDetailsRow, long>>)(r => r.GoodsReceiptDetailId);
}
