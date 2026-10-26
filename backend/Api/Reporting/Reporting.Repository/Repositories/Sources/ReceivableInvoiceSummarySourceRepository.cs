using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class ReceivableInvoiceSummaryRow
{
    public long DocumentId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

/// <summary>
/// The Receivable Invoice Summary for export, saved views and scheduled emails (TK-224). One row
/// for each posted invoice dated in the period, from the builders the page uses
/// (<see cref="ReceivableInvoiceSummaryReportRepository.Figures"/>, <c>ReceivedAmounts</c> and
/// <c>Rows</c>): its total in base currency and what is still due on the <c>to</c> date. A voided
/// invoice and a credit note are not listed. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class ReceivableInvoiceSummarySourceRepository : ReportSource<ReceivableInvoiceSummaryRow>
{
    public override string ReportKey => "receivable-invoice-summary";

    public override string Title => "Receivable Invoice Summary";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReceivableInvoiceSummaryRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<ReceivableInvoiceSummaryRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<ReceivableInvoiceSummaryRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<ReceivableInvoiceSummaryRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ReceivableInvoiceSummaryRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<ReceivableInvoiceSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new ReceivableInvoiceSummaryReportRequest { FromDate = start, ToDate = end };
        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(db.Invoices, db.CreditNotes, request, start, end);
        IQueryable<ReceivableInvoiceRow> rows = ReceivableInvoiceSummaryReportRepository.WithStatus(
            ReceivableInvoiceSummaryReportRepository.Rows(
                figures, ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(db.TransactionRatios, figures, end), end),
            null);

        return from r in rows
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new ReceivableInvoiceSummaryRow
               {
                   DocumentId = r.DocumentId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   TotalAmount = r.Total,
                   AmountDue = r.Due,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceivableInvoiceSummaryRow, long>>)(r => r.DocumentId);
}
