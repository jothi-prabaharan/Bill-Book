using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class ReceivableInvoiceSummaryRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class ReceivableInvoiceSummarySource : ReportSource<ReceivableInvoiceSummaryRow>
{
    public override string ReportKey => "receivable-invoice-summary";

    public override string Title => "Receivable Invoice Summary";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

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
        return from i in db.Invoices
               join c in db.Contacts on i.ContactId equals c.ContactId
               select new ReceivableInvoiceSummaryRow
               {
                   DocumentNo = i.DocumentNo,
                   DocumentDate = i.DocumentDate,
                   ContactName = c.DisplayName,
                   TotalAmount = i.TotalAmount,
                   AmountDue = i.TotalAmount // standard simple implementation used in stub reports
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReceivableInvoiceSummaryRow, DateOnly>>)(r => r.DocumentDate);
}
