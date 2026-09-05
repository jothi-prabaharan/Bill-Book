using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public class InvoiceDnPaymentCollectionRow
{
    public string ReceiveMoneyNo { get; set; } = null!;
    public DateOnly ReceiptDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public decimal Amount { get; set; }
}

public sealed class InvoiceDnPaymentCollectionSource : ReportSource<InvoiceDnPaymentCollectionRow>
{
    public override string ReportKey => "invoice-dn-payment-collection";

    public override string Title => "Invoice/DN Payment Collection";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

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
        return from rm in db.Set<ReceiveMoneyRead>()
               join rmd in db.Set<ReceiveMoneyDetailRead>() on rm.ReceiveMoneyId equals rmd.ReceiveMoneyId
               join c in db.Contacts on rm.ContactId equals c.ContactId
               join i in db.Invoices on rmd.MappingTransactionId equals i.InvoiceId
               where rmd.MappingTransactionTypeCode == "INV"
               select new InvoiceDnPaymentCollectionRow
               {
                   ReceiveMoneyNo = rm.TransactionNo ?? string.Empty,
                   ReceiptDate = rm.TransactionDate,
                   ContactName = c.DisplayName,
                   DocumentNo = i.DocumentNo,
                   Amount = rmd.Amount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<InvoiceDnPaymentCollectionRow, DateOnly>>)(r => r.ReceiptDate);
}
