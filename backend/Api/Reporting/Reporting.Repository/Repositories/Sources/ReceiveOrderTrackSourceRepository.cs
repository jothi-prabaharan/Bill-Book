using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class ReceiveOrderTrackRow
{
    public long GoodsReceiptId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal Value { get; set; }
    public decimal BilledValue { get; set; }
    public decimal ToBillValue { get; set; }
}

/// <summary>
/// The Receive Order Track Report for export, saved views and scheduled emails (TK-242). One row for each
/// posted goods receipt dated in the period, from the builders the page uses
/// (<see cref="ReceiveOrderTrackReportRepository.Rows"/>): its billing status, what its accepted goods are
/// worth, how much of that is billed and what is still to bill, in the branch's base currency. The
/// <c>status</c> column is that billing status. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class ReceiveOrderTrackSourceRepository : ReportSource<ReceiveOrderTrackRow>
{
    public override string ReportKey => "receive-order-track";
    public override string Title => "Receive Order Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReceiveOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<ReceiveOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<ReceiveOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<ReceiveOrderTrackRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<ReceiveOrderTrackRow, decimal>("value", ColumnDataType.Money, r => r.Value, AggregateFunction.Sum),
        ReportColumn.Of<ReceiveOrderTrackRow, decimal>("billedValue", ColumnDataType.Money, r => r.BilledValue, AggregateFunction.Sum),
        ReportColumn.Of<ReceiveOrderTrackRow, decimal>("toBillValue", ColumnDataType.Money, r => r.ToBillValue, AggregateFunction.Sum),
        ReportColumn.Of<ReceiveOrderTrackRow, long>("goodsReceiptId", ColumnDataType.Number, r => r.GoodsReceiptId, filterable: false),
    ];

    protected override IQueryable<ReceiveOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new ReceiveOrderTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<GoodsReceiptRead> receipts = ReceiveOrderTrackReportRepository.Receipts(db.GoodsReceipts, request, start, end);
        IQueryable<ReceiveOrderBilledSum> billed = ReceiveOrderTrackReportRepository.BilledSums(db.Bills, db.BillDetails);
        IQueryable<ReceiveOrderTrackEntry> rows = ReceiveOrderTrackReportRepository.Rows(
            receipts,
            ReceiveOrderTrackReportRepository.Figures(receipts, db.GoodsReceiptDetails),
            ReceiveOrderTrackReportRepository.BilledFigures(receipts, db.GoodsReceiptDetails, billed));

        return from r in rows
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new ReceiveOrderTrackRow
               {
                   GoodsReceiptId = r.GoodsReceiptId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = r.Status.ToString(),
                   Value = r.Value,
                   BilledValue = r.BilledValue,
                   ToBillValue = r.Value - r.BilledValue,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceiveOrderTrackRow, long>>)(r => r.GoodsReceiptId);
}
