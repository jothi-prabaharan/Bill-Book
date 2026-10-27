using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class PurchaseOrderTrackRow
{
    public long PurchaseOrderId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string FulfilmentStatus { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// The Purchase Order Track Report for export, saved views and scheduled emails (TK-240). One row for
/// each purchase order dated in the period but a cancelled one, from the builders the page uses
/// (<see cref="PurchaseOrderTrackReportRepository.Rows"/>): how far it has got and its total in the
/// branch's base currency. The <c>fulfilmentStatus</c> column is that progress. <b>Both dates are
/// required</b> (D-67).
/// </summary>
public sealed class PurchaseOrderTrackSourceRepository : ReportSource<PurchaseOrderTrackRow>
{
    public override string ReportKey => "purchase-order-track";
    public override string Title => "Purchase Order Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PurchaseOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<PurchaseOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PurchaseOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PurchaseOrderTrackRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<PurchaseOrderTrackRow, string>("fulfilmentStatus", ColumnDataType.Enum, r => r.FulfilmentStatus, groupable: true),
        ReportColumn.Of<PurchaseOrderTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseOrderTrackRow, long>("purchaseOrderId", ColumnDataType.Number, r => r.PurchaseOrderId, filterable: false),
    ];

    protected override IQueryable<PurchaseOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new PurchaseOrderTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<PurchaseOrderRead> orders = PurchaseOrderTrackReportRepository.Orders(db.PurchaseOrders, request, start, end);
        IQueryable<PurchaseBilledSum> billed = PurchaseOrderTrackReportRepository.BilledSums(
            PurchaseOrderTrackReportRepository.BilledLines(db.Bills, db.BillDetails, db.GoodsReceiptDetails));
        IQueryable<PurchaseOrderTrackEntry> rows = PurchaseOrderTrackReportRepository.WithProgress(
            PurchaseOrderTrackReportRepository.Rows(
                orders,
                PurchaseOrderTrackReportRepository.Figures(orders, db.PurchaseOrderDetails),
                PurchaseOrderTrackReportRepository.BilledFigures(orders, db.PurchaseOrderDetails, billed)),
            null);

        return from r in rows
               join o in orders on r.PurchaseOrderId equals o.PurchaseOrderId
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new PurchaseOrderTrackRow
               {
                   PurchaseOrderId = r.PurchaseOrderId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = o.Status.ToString(),
                   FulfilmentStatus = r.Progress.ToString(),
                   TotalAmount = o.TotalAmountBase,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseOrderTrackRow, long>>)(r => r.PurchaseOrderId);
}
