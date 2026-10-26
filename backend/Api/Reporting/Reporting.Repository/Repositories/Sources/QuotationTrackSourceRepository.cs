using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class QuotationTrackRow
{
    public long QuoteId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public QuoteOutcome Outcome { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// The Quotation Track Report for export, saved views and scheduled emails (TK-231). One row for each
/// quote dated in the period but a voided one, from the builders the page uses
/// (<see cref="QuotationTrackReportRepository.Rows"/>): its outcome as it stands today and its total in
/// the branch's base currency. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class QuotationTrackSourceRepository : ReportSource<QuotationTrackRow>
{
    public override string ReportKey => "quote-track";

    public override string Title => "Quotation Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<QuotationTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<QuotationTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<QuotationTrackRow, DateOnly>("validUntil", ColumnDataType.Date, r => r.ValidUntil),
        ReportColumn.Of<QuotationTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<QuotationTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<QuotationTrackRow, string>("outcome", ColumnDataType.Enum, r => r.Outcome.ToString(), groupable: true),
        ReportColumn.Of<QuotationTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<QuotationTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new QuotationTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<QuoteRead> quotes = QuotationTrackReportRepository.Quotes(db.Quotes, request, start, end);
        IQueryable<QuoteTrackRow> rows = QuotationTrackReportRepository.WithOutcome(
            QuotationTrackReportRepository.Rows(quotes, db.Invoices, db.SalesOrders, today), null);

        return from r in rows
               join q in quotes on r.QuoteId equals q.QuoteId
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new QuotationTrackRow
               {
                   QuoteId = r.QuoteId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ValidUntil = r.ValidUntil,
                   ContactName = c.DisplayName,
                   Status = q.Status,
                   Outcome = r.Outcome,
                   TotalAmount = r.Total,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<QuotationTrackRow, long>>)(r => r.QuoteId);
}
