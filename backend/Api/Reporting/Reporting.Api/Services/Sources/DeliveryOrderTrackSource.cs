using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class DeliveryOrderTrackRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
}

public sealed class DeliveryOrderTrackSource : ReportSource<DeliveryOrderTrackRow>
{
    public override string ReportKey => "delivery-challan-track";

    public override string Title => "Delivery Challan Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<DeliveryOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<DeliveryOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
    ];

    protected override IQueryable<DeliveryOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from d in db.DeliveryChallans
               join c in db.Contacts on d.ContactId equals c.ContactId
               select new DeliveryOrderTrackRow
               {
                   DocumentNo = d.DocumentNo,
                   DocumentDate = d.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = d.Status
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<DeliveryOrderTrackRow, DateOnly>>)(r => r.DocumentDate);
}
