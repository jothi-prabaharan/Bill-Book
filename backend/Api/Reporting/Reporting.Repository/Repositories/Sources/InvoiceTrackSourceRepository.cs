using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class InvoiceTrackRow
{
    public long DocumentId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public InvoiceTrackStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

/// <summary>
/// The Invoice Track Report for export, saved views and scheduled emails (TK-226). One row for
/// each invoice dated in the period but a voided one, from the builders the page uses
/// (<see cref="InvoiceTrackReportRepository.Entries"/>): where it stands, its total in base
/// currency and what is still due on the <c>to</c> date. A draft is listed with nothing due.
/// <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class InvoiceTrackSourceRepository : ReportSource<InvoiceTrackRow>
{
    public override string ReportKey => "invoice-track";

    public override string Title => "Invoice Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<InvoiceTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<InvoiceTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<InvoiceTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<InvoiceTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<InvoiceTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<InvoiceTrackRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<InvoiceTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new InvoiceTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<InvoiceTrackEntry> entries = InvoiceTrackReportRepository.WithStatus(
            InvoiceTrackReportRepository.Entries(db.Invoices, db.CreditNotes, db.TransactionRatios, request, start, end), null);

        return from e in entries
               join c in db.Contacts on e.ContactId equals c.ContactId
               select new InvoiceTrackRow
               {
                   DocumentId = e.DocumentId,
                   DocumentNo = e.DocumentNo,
                   DocumentDate = e.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = e.Status,
                   TotalAmount = e.Total,
                   AmountDue = e.Due,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<InvoiceTrackRow, long>>)(r => r.DocumentId);
}
