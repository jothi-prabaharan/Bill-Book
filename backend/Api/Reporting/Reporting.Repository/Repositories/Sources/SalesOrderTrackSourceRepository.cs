using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class SalesOrderTrackRow
{
    public long SalesOrderId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public SalesOrderProgress Progress { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// The Sales Order Track Report for export, saved views and scheduled emails (TK-230). One row for
/// each sales order dated in the period but a cancelled one, from the builders the page uses
/// (<see cref="SalesOrderTrackReportRepository.Rows"/>): how far it has got and its value before tax in
/// the branch's base currency. The <c>fulfilmentStatus</c> column is that progress. <b>Both dates are
/// required</b> (D-67).
/// </summary>
public sealed class SalesOrderTrackSourceRepository : ReportSource<SalesOrderTrackRow>
{
    public override string ReportKey => "sales-order-track";

    public override string Title => "Sales Order Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesOrderTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesOrderTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesOrderTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<SalesOrderTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<SalesOrderTrackRow, string>("fulfilmentStatus", ColumnDataType.Enum, r => r.Progress.ToString(), groupable: true),
        ReportColumn.Of<SalesOrderTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SalesOrderTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new SalesOrderTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<SalesOrderRead> orders = SalesOrderTrackReportRepository.Orders(db.SalesOrders, request, start, end);
        IQueryable<OrderTrackRow> rows = SalesOrderTrackReportRepository.WithProgress(
            SalesOrderTrackReportRepository.Rows(orders, SalesOrderTrackReportRepository.Figures(orders, db.SalesOrderDetails)), null);

        return from r in rows
               join o in orders on r.SalesOrderId equals o.SalesOrderId
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new SalesOrderTrackRow
               {
                   SalesOrderId = r.SalesOrderId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = o.Status,
                   Progress = r.Progress,
                   TotalAmount = o.TotalAmountBase,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesOrderTrackRow, long>>)(r => r.SalesOrderId);
}
