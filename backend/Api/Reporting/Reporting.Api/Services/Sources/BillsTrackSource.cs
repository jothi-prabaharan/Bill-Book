using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class BillsTrackRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class BillsTrackSource : ReportSource<BillsTrackRow>
{
    public override string ReportKey => "bills-track";
    public override string Title => "Bills Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BillsTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<BillsTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<BillsTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<BillsTrackRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<BillsTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<BillsTrackRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, AggregateFunction.Sum),
        ReportColumn.Of<BillsTrackRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    protected override IQueryable<BillsTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from b in db.Bills
               where (start == null || b.DocumentDate >= start) &&
                     (end == null || b.DocumentDate <= end)
               join c in db.Contacts on b.ContactId equals c.ContactId
               select new BillsTrackRow
               {
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = b.Status.ToString(),
                   TotalAmount = b.TotalAmount,
                   AmountDue = b.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BillsTrackRow, DateOnly>>)(r => r.DocumentDate);
}
