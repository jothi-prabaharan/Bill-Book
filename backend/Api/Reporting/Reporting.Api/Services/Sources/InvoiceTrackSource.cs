using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class InvoiceTrackRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class InvoiceTrackSource : ReportSource<InvoiceTrackRow>
{
    public override string ReportKey => "invoice-track";

    public override string Title => "Invoice Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

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
        return from i in db.Invoices
               join c in db.Contacts on i.ContactId equals c.ContactId
               select new InvoiceTrackRow
               {
                   DocumentNo = i.DocumentNo,
                   DocumentDate = i.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = i.Status,
                   TotalAmount = i.TotalAmount,
                   AmountDue = i.TotalAmount // standard simple implementation used in stub reports
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<InvoiceTrackRow, DateOnly>>)(r => r.DocumentDate);
}
