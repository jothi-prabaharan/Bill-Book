using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

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

public sealed class PurchaseOrderTrackSource : ReportSource<PurchaseOrderTrackRow>
{
    public override string ReportKey => "purchase-order-track";
    public override string Title => "Purchase Order Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
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
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from po in db.PurchaseOrders
               where (start == null || po.DocumentDate >= start) &&
                     (end == null || po.DocumentDate <= end)
               join c in db.Contacts on po.ContactId equals c.ContactId
               select new PurchaseOrderTrackRow
               {
                   PurchaseOrderId = po.PurchaseOrderId,
                   DocumentNo = po.DocumentNo,
                   DocumentDate = po.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = po.Status.ToString(),
                   FulfilmentStatus = po.FulfilmentStatus.ToString(),
                   TotalAmount = po.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseOrderTrackRow, DateOnly>>)(r => r.DocumentDate);
}
