using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class SalesOrderTrackRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public int FulfilmentStatus { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class SalesOrderTrackSource : ReportSource<SalesOrderTrackRow>
{
    public override string ReportKey => "sales-order-track";

    public override string Title => "Sales Order Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<SalesOrderTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<SalesOrderTrackRow, int>("fulfilmentStatus", ColumnDataType.Number, r => r.FulfilmentStatus),
        ReportColumn.Of<SalesOrderTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SalesOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.SalesOrders
               join c in db.Contacts on s.ContactId equals c.ContactId
               select new SalesOrderTrackRow
               {
                   DocumentNo = s.DocumentNo,
                   DocumentDate = s.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = s.Status,
                   FulfilmentStatus = s.FulfilmentStatus,
                   TotalAmount = s.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesOrderTrackRow, DateOnly>>)(r => r.DocumentDate);
}
