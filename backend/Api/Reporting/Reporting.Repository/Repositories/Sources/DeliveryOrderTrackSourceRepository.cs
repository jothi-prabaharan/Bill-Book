using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class DeliveryOrderTrackRow
{
    public long DeliveryChallanId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public ChallanType ChallanType { get; set; }
    public ChallanProgress Progress { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal ToBillValue { get; set; }
}

/// <summary>
/// The Delivery Order Track Report for export, saved views and scheduled emails (TK-232). One row for each
/// delivery challan dated in the period but a voided one, from the builders the page uses
/// (<see cref="DeliveryOrderTrackReportRepository.Rows"/>): why the goods left, how far the challan has been
/// billed, its value before tax in the branch's base currency and what of it is still to bill. <b>Both
/// dates are required</b> (D-67).
/// </summary>
public sealed class DeliveryOrderTrackSourceRepository : ReportSource<DeliveryOrderTrackRow>
{
    public override string ReportKey => "delivery-challan-track";

    public override string Title => "Delivery Challan Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<DeliveryOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<DeliveryOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("challanType", ColumnDataType.Enum, r => r.ChallanType.ToString(), groupable: true),
        ReportColumn.Of<DeliveryOrderTrackRow, string>("progress", ColumnDataType.Enum, r => r.Progress.ToString(), groupable: true),
        ReportColumn.Of<DeliveryOrderTrackRow, decimal>("deliveredValue", ColumnDataType.Money, r => r.DeliveredValue, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<DeliveryOrderTrackRow, decimal>("toBillValue", ColumnDataType.Money, r => r.ToBillValue, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<DeliveryOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new DeliveryOrderTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<DeliveryChallanRead> challans = DeliveryOrderTrackReportRepository.Challans(db.DeliveryChallans, request, start, end);
        IQueryable<ChallanTrackRow> rows = DeliveryOrderTrackReportRepository.WithProgress(
            DeliveryOrderTrackReportRepository.Rows(challans, DeliveryOrderTrackReportRepository.Figures(challans, db.DeliveryChallanDetails)), null);

        return from r in rows
               join d in challans on r.DeliveryChallanId equals d.DeliveryChallanId
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new DeliveryOrderTrackRow
               {
                   DeliveryChallanId = r.DeliveryChallanId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = d.Status,
                   ChallanType = r.ChallanType,
                   Progress = r.Progress,
                   DeliveredValue = r.DeliveredValue,
                   ToBillValue = r.DeliveredValue - r.InvoicedValue,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<DeliveryOrderTrackRow, long>>)(r => r.DeliveryChallanId);
}
