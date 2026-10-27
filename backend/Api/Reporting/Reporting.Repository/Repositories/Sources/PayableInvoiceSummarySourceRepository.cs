using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class PayableInvoiceSummaryRow
{
    /// <summary>The bill's id.</summary>
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

/// <summary>
/// The Payable Invoice Summary for export, saved views and scheduled emails (TK-235). One row for each posted bill
/// dated in the period, from the builders the page uses (<see cref="PayableInvoiceSummaryReportRepository.Figures"/>,
/// <c>PaidAmounts</c> and <c>Rows</c>): its total in base currency and what is still due on the <c>to</c> date, which is
/// the total less any TDS and, on a reverse-charge bill, the tax, and less what was paid. A voided bill and a debit note
/// are not listed. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class PayableInvoiceSummarySourceRepository : ReportSource<PayableInvoiceSummaryRow>
{
    public override string ReportKey => "payable-invoice-summary";
    public override string Title => "Payable Invoice Summary";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PayableInvoiceSummaryRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<PayableInvoiceSummaryRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PayableInvoiceSummaryRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PayableInvoiceSummaryRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceSummaryRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceSummaryRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false)
    ];

    protected override IQueryable<PayableInvoiceSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new PayableInvoiceSummaryReportRequest { FromDate = start, ToDate = end };
        IQueryable<PayableInvoiceFigure> figures = PayableInvoiceSummaryReportRepository.Figures(db.Bills, db.DebitNotes, request, start, end);
        IQueryable<PayableInvoiceRow> rows = PayableInvoiceSummaryReportRepository.WithStatus(
            PayableInvoiceSummaryReportRepository.Rows(
                figures, PayableInvoiceSummaryReportRepository.PaidAmounts(db.TransactionRatios, figures, end), end),
            null);

        return from r in rows
               join c in db.Contacts on r.ContactId equals c.ContactId
               select new PayableInvoiceSummaryRow
               {
                   BillId = r.DocumentId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactName = c.DisplayName,
                   TotalAmount = r.Total,
                   AmountDue = r.Due,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PayableInvoiceSummaryRow, long>>)(r => r.BillId);
}
