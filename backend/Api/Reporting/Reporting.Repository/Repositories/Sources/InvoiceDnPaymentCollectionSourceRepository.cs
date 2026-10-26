using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class InvoiceDnPaymentCollectionRow
{
    public long TransactionRatioId { get; set; }
    public string ReceiveMoneyNo { get; set; } = null!;
    public DateOnly ReceiptDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public decimal Amount { get; set; }
}

/// <summary>
/// The Invoice/DN Payment Collection Report for export, saved views and scheduled emails
/// (TK-227). One row for each invoice a posted receipt dated in the period settled, read from the
/// settlements that name the receipt as their source, as the page reads them; the amount is what
/// was settled, in the branch's base currency. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class InvoiceDnPaymentCollectionSourceRepository : ReportSource<InvoiceDnPaymentCollectionRow>
{
    public override string ReportKey => "invoice-dn-payment-collection";

    public override string Title => "Invoice/DN Payment Collection";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<InvoiceDnPaymentCollectionRow, string>("receiveMoneyNo", ColumnDataType.Text, r => r.ReceiveMoneyNo),
        ReportColumn.Of<InvoiceDnPaymentCollectionRow, DateOnly>("receiptDate", ColumnDataType.Date, r => r.ReceiptDate),
        ReportColumn.Of<InvoiceDnPaymentCollectionRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<InvoiceDnPaymentCollectionRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<InvoiceDnPaymentCollectionRow, decimal>("amount", ColumnDataType.Money, r => r.Amount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<InvoiceDnPaymentCollectionRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new Reporting.Entity.Models.InvoiceDnPaymentCollectionReportRequest { FromDate = start, ToDate = end };

        return from t in db.TransactionRatios
               join rm in InvoiceDnPaymentCollectionReportRepository.Receipts(db.ReceiveMoney, request, start, end)
                   on t.SourceTransactionId equals rm.ReceiveMoneyId
               join c in db.Contacts on rm.ContactId equals c.ContactId
               join i in db.Invoices on t.TargetTransactionId equals i.InvoiceId
               where !t.IsVoided && t.SourceTransactionTypeCode == "RCM"
                   && (t.TargetTransactionTypeCode == "INV" || t.TargetTransactionTypeCode == "POS")
               select new InvoiceDnPaymentCollectionRow
               {
                   TransactionRatioId = t.TransactionRatioId,
                   ReceiveMoneyNo = rm.TransactionNo ?? string.Empty,
                   ReceiptDate = rm.TransactionDate,
                   ContactName = c.DisplayName,
                   DocumentNo = i.DocumentNo,
                   Amount = rm.ExchangeRate > 0m && rm.ExchangeRate != 1m ? Math.Round(t.Amount / rm.ExchangeRate, 2) : t.Amount,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<InvoiceDnPaymentCollectionRow, long>>)(r => r.TransactionRatioId);
}
