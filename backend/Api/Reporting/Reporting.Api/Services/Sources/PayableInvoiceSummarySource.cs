using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public class PayableInvoiceSummaryRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class PayableInvoiceSummarySource : ReportSource<PayableInvoiceSummaryRow>
{
    public override string ReportKey => "payable-invoice-summary";
    public override string Title => "Payable Invoice Summary";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

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
        return from b in db.Bills
               join c in db.Contacts on b.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               select new PayableInvoiceSummaryRow
               {
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   ContactName = c != null ? c.DisplayName : "Unknown",
                   TotalAmount = b.TotalAmount,
                   AmountDue = b.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PayableInvoiceSummaryRow, DateOnly>>)(r => r.DocumentDate);
}
