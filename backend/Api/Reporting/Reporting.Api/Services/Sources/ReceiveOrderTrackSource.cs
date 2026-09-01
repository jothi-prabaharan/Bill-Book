using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class ReceiveOrderTrackRow
{
    public long GoodsReceiptId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public sealed class ReceiveOrderTrackSource : ReportSource<ReceiveOrderTrackRow>
{
    public override string ReportKey => "receive-order-track";
    public override string Title => "Receive Order Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReceiveOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<ReceiveOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<ReceiveOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<ReceiveOrderTrackRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<ReceiveOrderTrackRow, long>("goodsReceiptId", ColumnDataType.Number, r => r.GoodsReceiptId, filterable: false),
    ];

    protected override IQueryable<ReceiveOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from gr in db.GoodsReceipts
               where (start == null || gr.DocumentDate >= start) &&
                     (end == null || gr.DocumentDate <= end)
               join c in db.Contacts on gr.ContactId equals c.ContactId
               select new ReceiveOrderTrackRow
               {
                   GoodsReceiptId = gr.GoodsReceiptId,
                   DocumentNo = gr.DocumentNo,
                   DocumentDate = gr.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = gr.Status.ToString()
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceiveOrderTrackRow, DateOnly>>)(r => r.DocumentDate);
}
